using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.OData.Common.Exceptions;
using Microsoft.Extensions.Logging;
using PanoramicData.OData.Client.Exceptions;
using Polly.Retry;

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
            result => Result.Ok(result),
            cancellationToken).ConfigureAwait(false);
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
                var result = await operation().ConfigureAwait(false);
                return result as IList<TEntity> ?? result.ToList();
            },
            result => Result.Ok<IEnumerable<TEntity>>(result),
            cancellationToken).ConfigureAwait(false);
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
            result => Result.Ok(result),
            cancellationToken).ConfigureAwait(false);
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
                await operation().ConfigureAwait(false);
                return true;
            },
            _ => Result.Ok(),
            cancellationToken,
            treatNotFoundAsSuccess).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an operation that may return business-level errors (e.g., batch partial failures)
    /// without throwing exceptions. The operation returns <c>null</c> on success or a list of errors on failure.
    /// </summary>
    public async Task<Result> ExecuteNonQueryAsync(
        string operationName,
        Func<Task<List<IError>?>> operation,
        Func<object[]>? entityKey = null,
        CancellationToken cancellationToken = default)
    {
        var context = new OperationContext(operationName, _entityTypeName, entityKey?.Invoke());

        return await ExecuteWithRetryAsync(
            context,
            async () => await operation().ConfigureAwait(false),
            errors => errors is null ? Result.Ok() : Result.Fail(errors),
            cancellationToken).ConfigureAwait(false);
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
        where TResult : IResultBase
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

                    return await operation().ConfigureAwait(false);
                }, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                attemptCount = 1;
                result = await operation().ConfigureAwait(false);
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
                return LogSuppressed404AndReturnOk<TResult>(context, stopwatch.Elapsed, attemptCount, requestUrl: null);
            }

            return HandleNotFound<TResult>(context, stopwatch.Elapsed, ex, attemptCount);
        }
        catch (ODataClientException clientEx) when (clientEx.StatusCode == 404 && treatNotFoundAsSuccess)
        {
            stopwatch.Stop();
            return LogSuppressed404AndReturnOk<TResult>(context, stopwatch.Elapsed, attemptCount, clientEx.RequestUrl);
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
        where TResult : IResultBase
    {
        if (exception is ODataPayloadValidationException validationEx)
        {
            _logger.LogWarning(validationEx,
                "{Operation} on {EntityType} failed payload validation: {Message}",
                context.OperationName, context.EntityType, validationEx.Message);

            var validationError = new IntegrationError(
                $"{context.EntityType}.RequiredFieldMissing",
                validationEx.Message,
                ErrorType.Validation,
                validationEx);
            return CreateFailResult<TResult>(validationError);
        }

        var error = exception switch
        {
            ODataUnauthorizedException unauthorizedEx
                => CreateODataClientError(context, elapsed, (int)HttpStatusCode.Unauthorized, unauthorizedEx, attemptCount),
            ODataForbiddenException forbiddenEx
                => CreateODataClientError(context, elapsed, (int)HttpStatusCode.Forbidden, forbiddenEx, attemptCount),
            ODataConcurrencyException concurrencyEx
                => CreateODataClientError(context, elapsed, (int)HttpStatusCode.PreconditionFailed, concurrencyEx, attemptCount),
            ODataClientException clientEx
                => CreateODataClientError(context, elapsed, clientEx.StatusCode, clientEx, attemptCount),
            TaskCanceledException tcEx when !cancellationToken.IsCancellationRequested
                => CreateTimeoutError(context, elapsed, tcEx, attemptCount),
            OperationCanceledException ocEx => CreateCancellationError(context, elapsed, ocEx, attemptCount),
            _ => CreateUnexpectedError(context, elapsed, exception, attemptCount)
        };

        return CreateFailResult<TResult>(error);
    }

    private IntegrationError CreateODataClientError(
        OperationContext context,
        TimeSpan elapsed,
        int? statusCode,
        Exception exception,
        int attemptCount)
    {
        var code = statusCode ?? 0;

        string? responseBody = null;
        string? requestUrl = null;
        if (exception is ODataClientException clientException)
        {
            responseBody = clientException.ResponseBody;
            requestUrl = clientException.RequestUrl;
        }

        var innerErrorDetail = ExtractD365InnerError(responseBody);

        var (errorCode, errorMessage, errorType) = code switch
        {
            401 or 403 =>
                ("Unauthorized", "Authentication or authorisation failure", ErrorType.Failure),
            400 =>
                ("ValidationFailed",
                    innerErrorDetail is not null
                        ? $"Validation failed: {innerErrorDetail}"
                        : $"Validation failed: {exception.Message}",
                    ErrorType.Validation),
            404 =>
                ("NotFound", "Entity was not found", ErrorType.NotFound),
            409 =>
                ("Conflict", $"Conflict occurred: {exception.Message}", ErrorType.Conflict),
            412 =>
                ("ConcurrencyConflict", "Entity modified by another user", ErrorType.Conflict),
            429 =>
                ("RateLimitExceeded", "Rate limit exceeded", ErrorType.Failure),
            503 or 504 =>
                ("ServiceUnavailable", $"Service unavailable: {exception.Message}", ErrorType.Failure),
            >= 500 =>
                ("ServerError",
                    innerErrorDetail is not null
                        ? $"Server error: {innerErrorDetail}"
                        : $"Server error: {exception.Message}",
                    ErrorType.Failure),
            _ => ($"{context.OperationName}Failed", $"Operation failed: {exception.Message}", ErrorType.Failure)
        };

        var logLevel = errorType == ErrorType.Validation ? LogLevel.Warning : LogLevel.Error;

        if (responseBody is not null)
        {
            const int maxResponseBodyLength = 1024;
            string truncatedBody = responseBody.Length > maxResponseBodyLength
                ? responseBody[..maxResponseBodyLength] + "...(truncated)"
                : responseBody;

            _logger.Log(logLevel, exception,
                "{Operation} on {EntityType} failed after {ElapsedMs}ms and {Attempts} attempt(s). " +
                "StatusCode: {StatusCode}, ResponseBody: {ResponseBody}",
                context.OperationName, context.EntityType, elapsed.TotalMilliseconds,
                attemptCount, statusCode, truncatedBody);
        }
        else
        {
            _logger.Log(logLevel, exception,
                "{Operation} on {EntityType} failed after {ElapsedMs}ms and {Attempts} attempt(s). " +
                "StatusCode: {StatusCode}",
                context.OperationName, context.EntityType, elapsed.TotalMilliseconds,
                attemptCount, statusCode);
        }

        return new IntegrationError($"{context.EntityType}.{errorCode}", errorMessage, errorType, exception);
    }

    /// <summary>
    /// Extracts the inner error message from a D365 F&amp;O OData error response body.
    /// D365 returns errors in the format: <c>{"error":{"message":"...","innererror":{"message":"..."}}}</c>.
    /// The inner error message typically contains field names and constraint details.
    /// </summary>
    internal static string? ExtractD365InnerError(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("error", out JsonElement errorElement))
            {
                // Try innererror.message first (most specific)
                if (errorElement.TryGetProperty("innererror", out JsonElement innerError) &&
                    innerError.TryGetProperty("message", out JsonElement innerMessage))
                {
                    string innerText = innerMessage.GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(innerText))
                    {
                        return innerText;
                    }
                }

                // Fall back to error.message
                if (errorElement.TryGetProperty("message", out JsonElement message))
                {
                    string messageText = message.GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(messageText))
                    {
                        return messageText;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // APIM sometimes emits malformed JSON with unescaped quotes inside the error string
            // (observed 2026-04-14: `{"error": "The provided value for "d365foenvironment" is not
            // valid..."}`). Fall back to a best-effort lenient extractor so the consumer still
            // sees a meaningful message instead of the generic "Request failed with status ...".
            return ExtractLenientErrorMessage(responseBody);
        }

        return null;
    }

    /// <summary>
    /// Best-effort fallback extractor for malformed JSON error bodies. Looks for the first
    /// <c>"error"</c> or <c>"message"</c> key and returns the string value associated with it,
    /// terminating at the first quote that is followed by a structural JSON delimiter
    /// (<c>,</c> or <c>}</c>). Returns null if no recognisable fragment is found.
    /// </summary>
    internal static string? ExtractLenientErrorMessage(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        // Locate either "error" or "message" as the key. Prefer the inner "message" when both
        // appear (most specific), else "error" which is the common top-level key.
        int keyIndex = responseBody.IndexOf("\"message\"", StringComparison.Ordinal);
        if (keyIndex < 0)
        {
            keyIndex = responseBody.IndexOf("\"error\"", StringComparison.Ordinal);
        }
        if (keyIndex < 0)
        {
            return null;
        }

        // Skip to the first colon after the key.
        int colonIndex = responseBody.IndexOf(':', keyIndex);
        if (colonIndex < 0 || colonIndex >= responseBody.Length - 1)
        {
            return null;
        }

        // Find the first opening quote after the colon — that marks the start of the value.
        int valueStart = responseBody.IndexOf('"', colonIndex + 1);
        if (valueStart < 0 || valueStart >= responseBody.Length - 1)
        {
            return null;
        }
        valueStart++;

        // Bound the scan to the top-level closing brace so we never run past the JSON object.
        int lastBrace = responseBody.LastIndexOf('}');
        if (lastBrace <= valueStart)
        {
            return null;
        }

        // Scan forward for the first quote that is immediately followed (ignoring whitespace)
        // by a structural JSON delimiter — either ',' (next property starts) or '}' (object
        // ends). This handles two malformed shapes:
        //   {"error": "text with "unescaped" quotes"}     → terminates at the final "}
        //   {"error": "x", "details": "y"}                → terminates at the first ","
        // Quotes that appear mid-value without a following delimiter are treated as content.
        int valueEnd = -1;
        for (int i = valueStart; i <= lastBrace; i++)
        {
            if (responseBody[i] != '"')
            {
                continue;
            }

            int next = i + 1;
            while (next < responseBody.Length && char.IsWhiteSpace(responseBody[next]))
            {
                next++;
            }
            if (next < responseBody.Length && (responseBody[next] == ',' || responseBody[next] == '}'))
            {
                valueEnd = i;
                break;
            }
        }
        if (valueEnd <= valueStart)
        {
            return null;
        }

        string extracted = responseBody[valueStart..valueEnd].Trim();
        return string.IsNullOrWhiteSpace(extracted) ? null : extracted;
    }

    /// <summary>
    /// Emits a Warning and returns a success Result for a 404 that the caller opted to suppress
    /// via <c>treatNotFoundAsSuccess</c>. An HTTP 404 suppressed this way is normal for
    /// idempotent deletes, but it can also hide a silent failure where the request URL itself
    /// was malformed — hence the Warning level and the request URL in the structured log so a
    /// human reviewing the logs can distinguish "entity genuinely gone" from "client built the
    /// wrong URL". <paramref name="requestUrl"/> is null when the source was an internal
    /// <see cref="ODataNotFoundException"/> (no HTTP attempt was tied to a URL).
    /// </summary>
    private TResult LogSuppressed404AndReturnOk<TResult>(
        OperationContext context,
        TimeSpan elapsed,
        int attemptCount,
        string? requestUrl)
        where TResult : IResultBase
    {
        _logger.LogWarning(
            "{Operation} on {EntityType} returned HTTP 404 and is being treated as success " +
            "because treatNotFoundAsSuccess is enabled. RequestUrl: {RequestUrl}. " +
            "This may indicate a malformed request URL rather than a missing entity. " +
            "Duration: {ElapsedMs}ms, Attempts: {Attempts}",
            context.OperationName, context.EntityType, requestUrl ?? "(internal)",
            elapsed.TotalMilliseconds, attemptCount);

        return (TResult)(object)Result.Ok();
    }

    private TResult HandleNotFound<TResult>(
        OperationContext context,
        TimeSpan elapsed,
        ODataNotFoundException exception,
        int attemptCount)
        where TResult : IResultBase
    {
        _logger.LogInformation(
            "{Operation} on {EntityType} - entity not found after {ElapsedMs}ms " +
            "and {Attempts} attempt(s). Key: {@Key}",
            context.OperationName, context.EntityType, elapsed.TotalMilliseconds,
            attemptCount, context.EntityKey);

        var error = new IntegrationError(
            $"{context.EntityType}.NotFound",
            exception.Message,
            ErrorType.NotFound,
            exception);

        return CreateFailResult<TResult>(error);
    }

    private IntegrationError CreateTimeoutError(
        OperationContext context,
        TimeSpan elapsed,
        TaskCanceledException exception,
        int attemptCount)
    {
        _logger.LogError(exception,
            "{Operation} on {EntityType} timed out after {ElapsedMs}ms and {Attempts} attempt(s)",
            context.OperationName, context.EntityType, elapsed.TotalMilliseconds, attemptCount);

        return new IntegrationError(
            $"{context.EntityType}.Timeout",
            "Request timed out",
            ErrorType.Failure,
            exception);
    }

    private IntegrationError CreateCancellationError(
        OperationContext context,
        TimeSpan elapsed,
        OperationCanceledException exception,
        int attemptCount)
    {
        _logger.LogInformation(
            "{Operation} on {EntityType} was cancelled after {ElapsedMs}ms and {Attempts} attempt(s)",
            context.OperationName, context.EntityType, elapsed.TotalMilliseconds, attemptCount);

        return new IntegrationError(
            $"{context.EntityType}.Cancelled",
            "Operation was cancelled",
            ErrorType.Failure,
            exception);
    }

    private IntegrationError CreateUnexpectedError(
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

        return new IntegrationError(
            $"{context.EntityType}.UnexpectedError",
            $"An unexpected error occurred: {exception.Message}",
            ErrorType.Failure,
            exception);
    }

    private static TResult CreateFailResult<TResult>(IError error) where TResult : IResultBase
        => ResultFactory.FailFromError<TResult>(error);

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
