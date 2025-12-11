using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Results;
using Microsoft.Extensions.Logging;
using Polly.Retry;
using Simple.OData.Client;
using System.Diagnostics;
using System.Net;

namespace IntegratoR.OData.Common.Services;

/// <summary>
/// Handles exception processing and retry logic for OData operations.
/// Provides centralized error handling with comprehensive logging and performance metrics.
/// </summary>
/// <typeparam name="TEntity">The entity type that implements <see cref="IEntity"/>.</typeparam>
/// <remarks>
/// This handler abstracts all exception handling logic for OData operations, providing:
/// - Automatic retry for transient failures using Polly
/// - Comprehensive exception mapping to Result pattern
/// - Structured logging with performance tracking
/// - Support for different operation types (single, collection, scalar, non-query)
/// </remarks>
public class ODataExceptionHandler<TEntity> where TEntity : class, IEntity
{
    private readonly ILogger _logger;
    private readonly string _entityTypeName;
    private readonly AsyncRetryPolicy? _retryPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataExceptionHandler{TEntity}"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="retryPolicy">Optional Polly retry policy for automatic retries.</param>
    public ODataExceptionHandler(ILogger logger, AsyncRetryPolicy? retryPolicy = null)
    {
        _logger = logger;
        _entityTypeName = typeof(TEntity).Name;
        _retryPolicy = retryPolicy;
    }

    /// <summary>
    /// Executes an operation that returns a single entity with automatic retry support.
    /// </summary>
    public async Task<Result<TEntity>> ExecuteAsync(
        string operationName,
        Func<Task<TEntity>> operation,
        Func<object[]>? entityKey = null,
        CancellationToken cancellationToken = default)
    {
        var context = new OperationContext(operationName, _entityTypeName, entityKey?.Invoke());

        return await ExecuteWithRetryAsync(
            context,
            operation,
            result => Result<TEntity>.Ok(result),
            cancellationToken);
    }

    /// <summary>
    /// Executes an operation that returns a collection of entities with automatic retry support.
    /// </summary>
    public async Task<Result<IEnumerable<TEntity>>> ExecuteCollectionAsync(
        string operationName,
        Func<Task<IEnumerable<TEntity>>> operation,
        Func<object[]>? entityKey = null,
        CancellationToken cancellationToken = default)
    {
        var context = new OperationContext(operationName, _entityTypeName, entityKey?.Invoke());

        return await ExecuteWithRetryAsync(
            context,
            async () =>
            {
                var result = await operation();
                return result as IList<TEntity> ?? result.ToList();
            },
            result =>
            {
                LogSuccess(context, TimeSpan.Zero, result.Count);
                return Result<IEnumerable<TEntity>>.Ok(result);
            },
            cancellationToken);
    }

    /// <summary>
    /// Executes an operation that returns a scalar value with automatic retry support.
    /// </summary>
    public async Task<Result<T>> ExecuteScalarAsync<T>(
        string operationName,
        Func<Task<T>> operation,
        Func<object[]>? entityKey = null,
        CancellationToken cancellationToken = default)
    {
        var context = new OperationContext(operationName, _entityTypeName, entityKey?.Invoke());

        return await ExecuteWithRetryAsync(
            context,
            operation,
            result => Result<T>.Ok(result),
            cancellationToken);
    }

    /// <summary>
    /// Executes an operation that doesn't return a value with automatic retry support.
    /// </summary>
    public async Task<Result> ExecuteNonQueryAsync(
        string operationName,
        Func<Task> operation,
        Func<object[]>? entityKey = null,
        CancellationToken cancellationToken = default,
        bool treatNotFoundAsSuccess = false)
    {
        var context = new OperationContext(operationName, _entityTypeName, entityKey?.Invoke());

        return await ExecuteWithRetryAsync(
            context,
            async () =>
            {
                await operation();
                return true;
            },
            _ => Result.Ok(),
            cancellationToken,
            treatNotFoundAsSuccess);
    }

    /// <summary>
    /// Core retry wrapper that integrates Polly policies with exception handling.
    /// </summary>
    private async Task<TResult> ExecuteWithRetryAsync<TOperationResult, TResult>(
        OperationContext context,
        Func<Task<TOperationResult>> operation,
        Func<TOperationResult, TResult> resultMapper,
        CancellationToken cancellationToken,
        bool treatNotFoundAsSuccess = false)
        where TResult : IResult
    {
        var stopwatch = Stopwatch.StartNew();
        var attemptCount = 0;

        try
        {
            TOperationResult result;

            if (_retryPolicy != null)
            {
                result = await _retryPolicy.ExecuteAsync(async (ctx) =>
                {
                    attemptCount++;

                    if (attemptCount > 1)
                    {
                        _logger.LogInformation(
                            "Retry attempt {AttemptCount} for {Operation} on {EntityType}",
                            attemptCount, context.OperationName, context.EntityType);
                    }

                    return await operation();
                }, cancellationToken);
            }
            else
            {
                attemptCount = 1;
                result = await operation();
            }

            stopwatch.Stop();
            LogSuccess(context, stopwatch.Elapsed, attemptCount: attemptCount);

            return resultMapper(result);
        }
        catch (ODataNotFoundException ex)
        {
            stopwatch.Stop();

            if (treatNotFoundAsSuccess)
            {
                _logger.LogInformation(
                    "{Operation} on {EntityType} - entity not found (treating as success). " +
                    "Duration: {ElapsedMs}ms, Attempts: {Attempts}",
                    context.OperationName, context.EntityType,
                    stopwatch.ElapsedMilliseconds, attemptCount);

                return (TResult)(IResult)Result.Ok();
            }

            return HandleNotFound<TResult>(context, stopwatch.Elapsed, ex, attemptCount);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HandleException<TResult>(context, stopwatch.Elapsed, ex, cancellationToken, attemptCount);
        }
    }

    private TResult HandleException<TResult>(
        OperationContext context,
        TimeSpan elapsed,
        Exception exception,
        CancellationToken cancellationToken,
        int attemptCount)
        where TResult : IResult
    {
        var error = exception switch
        {
            WebRequestException webEx => CreateWebRequestError(context, elapsed, webEx, attemptCount),
            TaskCanceledException tcEx when !cancellationToken.IsCancellationRequested
                => CreateTimeoutError(context, elapsed, tcEx, attemptCount),
            OperationCanceledException ocEx => CreateCancellationError(context, elapsed, ocEx, attemptCount),
            _ => CreateUnexpectedError(context, elapsed, exception, attemptCount)
        };

        if (typeof(TResult).IsGenericType)
        {
            var genericType = typeof(TResult).GetGenericArguments()[0];
            var failMethod = typeof(Result<>)
                .MakeGenericType(genericType)
                .GetMethod(nameof(Result<object>.Fail), new[] { typeof(Error) });
            return (TResult)failMethod!.Invoke(null, new object[] { error })!;
        }

        return (TResult)(IResult)Result.Fail(error);
    }

    private Error CreateWebRequestError(
        OperationContext context,
        TimeSpan elapsed,
        WebRequestException exception,
        int attemptCount)
    {
        var (errorCode, errorMessage, errorType) = exception.Code switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                ("Unauthorized", $"Authentication failed: {exception.Message}", ErrorType.Failure),
            HttpStatusCode.BadRequest =>
                ("ValidationFailed", $"Validation failed: {exception.Message}", ErrorType.Validation),
            HttpStatusCode.NotFound =>
                ("NotFound", "Entity was not found", ErrorType.NotFound),
            HttpStatusCode.Conflict =>
                ("Conflict", $"Conflict occurred: {exception.Message}", ErrorType.Conflict),
            HttpStatusCode.PreconditionFailed =>
                ("ConcurrencyConflict", "Entity modified by another user", ErrorType.Conflict),
            HttpStatusCode.TooManyRequests =>
                ("RateLimitExceeded", "Rate limit exceeded", ErrorType.Failure),
            HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout =>
                ("ServiceUnavailable", $"Service unavailable: {exception.Message}", ErrorType.Failure),
            _ when ((int)exception.Code >= 500) =>
                ("ServerError", $"Server error: {exception.Message}", ErrorType.Failure),
            _ => ($"{context.OperationName}Failed", $"Operation failed: {exception.Message}", ErrorType.Failure)
        };

        var logLevel = errorType == ErrorType.Validation ? LogLevel.Warning : LogLevel.Error;

        _logger.Log(logLevel, exception,
            "{Operation} on {EntityType} failed after {ElapsedMs}ms and {Attempts} attempt(s). " +
            "StatusCode: {StatusCode}",
            context.OperationName, context.EntityType, elapsed.TotalMilliseconds,
            attemptCount, exception.Code);

        return new Error($"{context.EntityType}.{errorCode}", errorMessage, errorType, exception);
    }

    private TResult HandleNotFound<TResult>(
        OperationContext context,
        TimeSpan elapsed,
        ODataNotFoundException exception,
        int attemptCount)
        where TResult : IResult
    {
        _logger.LogInformation(
            "{Operation} on {EntityType} - entity not found after {ElapsedMs}ms " +
            "and {Attempts} attempt(s). Key: {@Key}",
            context.OperationName, context.EntityType, elapsed.TotalMilliseconds,
            attemptCount, context.EntityKey);

        var error = new Error(
            $"{context.EntityType}.NotFound",
            exception.Message,
            ErrorType.NotFound,
            exception);

        if (typeof(TResult).IsGenericType)
        {
            var genericType = typeof(TResult).GetGenericArguments()[0];
            var failMethod = typeof(Result<>)
                .MakeGenericType(genericType)
                .GetMethod(nameof(Result<object>.Fail), new[] { typeof(Error) });
            return (TResult)failMethod!.Invoke(null, new object[] { error })!;
        }

        return (TResult)(IResult)Result.Fail(error);
    }

    private Error CreateTimeoutError(
        OperationContext context,
        TimeSpan elapsed,
        TaskCanceledException exception,
        int attemptCount)
    {
        _logger.LogError(exception,
            "{Operation} on {EntityType} timed out after {ElapsedMs}ms and {Attempts} attempt(s)",
            context.OperationName, context.EntityType, elapsed.TotalMilliseconds, attemptCount);

        return new Error(
            $"{context.EntityType}.Timeout",
            "Request timed out",
            ErrorType.Failure,
            exception);
    }

    private Error CreateCancellationError(
        OperationContext context,
        TimeSpan elapsed,
        OperationCanceledException exception,
        int attemptCount)
    {
        _logger.LogInformation(
            "{Operation} on {EntityType} was cancelled after {ElapsedMs}ms and {Attempts} attempt(s)",
            context.OperationName, context.EntityType, elapsed.TotalMilliseconds, attemptCount);

        return new Error(
            $"{context.EntityType}.Cancelled",
            "Operation was cancelled",
            ErrorType.Failure,
            exception);
    }

    private Error CreateUnexpectedError(
        OperationContext context,
        TimeSpan elapsed,
        Exception exception,
        int attemptCount)
    {
        _logger.LogError(exception,
            "{Operation} on {EntityType} failed with unexpected exception after {ElapsedMs}ms " +
            "and {Attempts} attempt(s): {ExceptionType}",
            context.OperationName, context.EntityType, elapsed.TotalMilliseconds,
            attemptCount, exception.GetType().Name);

        return new Error(
            $"{context.EntityType}.UnexpectedError",
            $"An unexpected error occurred: {exception.Message}",
            ErrorType.Failure,
            exception);
    }

    private void LogSuccess(
        OperationContext context,
        TimeSpan elapsed,
        int? count = null,
        int attemptCount = 1)
    {
        var attemptInfo = attemptCount > 1 ? $" (after {attemptCount} attempts)" : "";

        if (count.HasValue)
        {
            _logger.LogInformation(
                "{Operation} on {EntityType} succeeded in {ElapsedMs}ms{AttemptInfo}. " +
                "Retrieved {Count} entities",
                context.OperationName, context.EntityType, elapsed.TotalMilliseconds,
                attemptInfo, count.Value);
        }
        else
        {
            _logger.LogInformation(
                "{Operation} on {EntityType} succeeded in {ElapsedMs}ms{AttemptInfo}",
                context.OperationName, context.EntityType, elapsed.TotalMilliseconds, attemptInfo);
        }
    }
}

/// <summary>
/// Encapsulates operation context information for structured logging.
/// </summary>
internal record OperationContext(
    string OperationName,
    string EntityType,
    object[]? EntityKey = null);

/// <summary>
/// Exception thrown when an OData entity is not found.
/// Used to distinguish NotFound scenarios from other errors for proper handling.
/// </summary>
internal class ODataNotFoundException : Exception
{
    public ODataNotFoundException(string message) : base(message) { }
}
