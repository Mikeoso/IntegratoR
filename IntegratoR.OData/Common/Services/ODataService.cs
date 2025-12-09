using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.OData.Common.Annotations;
using IntegratoR.OData.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Simple.OData.Client;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text.Json.Serialization;

namespace IntegratoR.OData.Common.Services;

/// <summary>
/// A generic service that provides a concrete implementation for data access operations
/// against a D365 F&O OData endpoint using Simple.OData.Client.
/// </summary>
/// <typeparam name="TEntity">The type of the entity, which must be a class implementing <see cref="IEntity{TKey}"/>.</typeparam>
/// <remarks>
/// This class serves as the default repository for all entities in the system. It handles
/// CRUD operations, complex queries, and batch operations. It also encapsulates error handling,
/// catching <see cref="WebRequestException"/> from the OData client and converting them into
/// the application's standard <see cref="Result"/> pattern for consistent error propagation.
/// </remarks>
public class ODataService<TEntity> : IODataService<TEntity>, IODataBatchService<TEntity> where TEntity : class, IEntity
{
    private readonly IODataClient _client;
    private readonly ILogger<ODataService<TEntity>> _logger;
    private readonly ODataExceptionHandler<TEntity> _exceptionHandler;

    public ODataService(IODataClient client, ILogger<ODataService<TEntity>> logger)
    {
        _client = client;
        _logger = logger;
        _exceptionHandler = new ODataExceptionHandler<TEntity>(logger);
    }

    #region IService Implementation

    /// <inheritdoc />
    public Task<Result<TEntity>> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return _exceptionHandler.ExecuteAsync(
            operationName: "Add",
            operation: async () =>
            {
                var payload = CreatePayload(entity, isCreateOperation: true);
                _logger.LogDebug("Adding entity {EntityType} with payload: {@Payload}", typeof(TEntity).Name, payload);

                return await _client
                    .For<TEntity>()
                    .Set(payload)
                    .InsertEntryAsync(true, cancellationToken);
            },
            entityKey: () => entity.GetCompositeKey(),
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<IEnumerable<TEntity>>> FindAsync(Expression<Func<TEntity, bool>>? filter, CancellationToken cancellationToken = default)
    {
        return _exceptionHandler.ExecuteCollectionAsync(
            operationName: "Find",
            operation: async () =>
            {
                var query = _client.For<TEntity>();

                if (filter is not null)
                {
                    query = query.Filter(filter);
                    _logger.LogDebug("Executing FindAsync for {EntityType} with filter: {Filter}",
                        typeof(TEntity).Name, filter.ToString());
                }

                return await query.FindEntriesAsync(cancellationToken);
            },
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<TEntity>> GetByKeyAsync(object[] keyValues, CancellationToken cancellationToken = default)
    {
        if (keyValues == null || keyValues.Length == 0)
        {
            return Task.FromResult(Result<TEntity>.Fail(new Error(
                $"{typeof(TEntity).Name}.InvalidKey",
                "Key values cannot be null or empty",
                ErrorType.Validation)));
        }

        return _exceptionHandler.ExecuteAsync(
            operationName: "GetByKey",
            operation: async () =>
            {
                _logger.LogDebug("Retrieving {EntityType} by key: {@KeyValues}", typeof(TEntity).Name, keyValues);

                var entity = await _client
                    .For<TEntity>()
                    .Key(keyValues)
                    .FindEntryAsync(cancellationToken);

                if (entity is null)
                {
                    throw new ODataNotFoundException("Entity with the specified composite key was not found");
                }

                return entity;
            },
            entityKey: () => keyValues,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<TEntity>> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity == null)
        {
            return Task.FromResult(Result<TEntity>.Fail(new Error(
                "Validation.NullEntity",
                "The provided entity cannot be null",
                ErrorType.Validation)));
        }

        return _exceptionHandler.ExecuteAsync(
            operationName: "Update",
            operation: async () =>
            {
                _logger.LogDebug("Updating {EntityType} with key {@Key}",
                    typeof(TEntity).Name, entity.GetCompositeKey());

                return await _client
                    .For<TEntity>()
                    .Key(entity)
                    .Set(entity)
                    .UpdateEntryAsync(cancellationToken);
            },
            entityKey: () => entity.GetCompositeKey(),
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity == null)
        {
            return Task.FromResult(Result.Fail(new Error(
                "Validation.NullEntity",
                "The provided entity cannot be null",
                ErrorType.Validation)));
        }

        return _exceptionHandler.ExecuteNonQueryAsync(
            operationName: "Delete",
            operation: async () =>
            {
                _logger.LogDebug("Deleting {EntityType} with key {@Key}",
                    typeof(TEntity).Name, entity.GetCompositeKey());

                await _client
                    .For<TEntity>()
                    .Key(entity)
                    .DeleteEntryAsync(cancellationToken);
            },
            entityKey: () => entity.GetCompositeKey(),
            cancellationToken: cancellationToken,
            treatNotFoundAsSuccess: true); // DELETE is idempotent
    }

    #endregion

    #region IODataService Implementation

    public Task<Result<IEnumerable<TEntity>>> QueryAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Expression<Func<TEntity, object>>? expand = null,
        Expression<Func<TEntity, object>>? select = null,
        int? skip = null,
        int? top = null,
        CancellationToken cancellationToken = default)
    {
        return _exceptionHandler.ExecuteCollectionAsync(
            operationName: "Query",
            operation: async () =>
            {
                var query = _client.For<TEntity>();

                if (filter is not null) query = query.Filter(filter);
                if (expand is not null) query = query.Expand(expand);
                if (select is not null) query = query.Select(select);
                if (skip.HasValue) query = query.Skip(skip.Value);
                if (top.HasValue) query = query.Top(top.Value);

                return await query.FindEntriesAsync(cancellationToken);
            },
            cancellationToken: cancellationToken);
    }

    public Task<Result<IEnumerable<TEntity>>> FindAll(CancellationToken cancellationToken = default)
    {
        return _exceptionHandler.ExecuteCollectionAsync(
            operationName: "FindAll",
            operation: async () => await _client.For<TEntity>().FindEntriesAsync(cancellationToken),
            cancellationToken: cancellationToken);
    }

    public Task<Result<int>> CountAsync(Expression<Func<TEntity, bool>>? filter = null, CancellationToken cancellationToken = default)
    {
        return _exceptionHandler.ExecuteScalarAsync(
            operationName: "Count",
            operation: async () =>
            {
                var query = _client.For<TEntity>();
                if (filter is not null) query = query.Filter(filter);
                return await query.Count().FindScalarAsync<int>(cancellationToken);
            },
            cancellationToken: cancellationToken);
    }

    #endregion

    #region IODataBatchService Implementation

    public Task<Result> AddBatchAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        return _exceptionHandler.ExecuteNonQueryAsync(
            operationName: "AddBatch",
            operation: async () =>
            {
                var batch = new ODataBatch(_client);
                foreach (var entity in entities)
                {
                    batch += c => c.For<TEntity>().Set(entity).InsertEntryAsync(cancellationToken);
                }
                await batch.ExecuteAsync(cancellationToken);
            },
            entityKey: () => new object[] { $"{entities.Count()} entities" },
            cancellationToken: cancellationToken);
    }

    public Task<Result> DeleteBatchAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        return _exceptionHandler.ExecuteNonQueryAsync(
            operationName: "DeleteBatch",
            operation: async () =>
            {
                var batch = new ODataBatch(_client);
                foreach (var entity in entities)
                {
                    batch += c => c.For<TEntity>().Key(entity.GetCompositeKey()).DeleteEntryAsync(cancellationToken);
                }
                await batch.ExecuteAsync(cancellationToken);
            },
            entityKey: () => new object[] { $"{entities.Count()} entities" },
            cancellationToken: cancellationToken);
    }

    public Task<Result> UpdateBatchAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        return _exceptionHandler.ExecuteNonQueryAsync(
            operationName: "UpdateBatch",
            operation: async () =>
            {
                var batch = new ODataBatch(_client);
                foreach (var entity in entities)
                {
                    batch += c => c.For<TEntity>().Key(entity.GetCompositeKey()).Set(entity).UpdateEntryAsync(cancellationToken);
                }
                await batch.ExecuteAsync(cancellationToken);
            },
            entityKey: () => new object[] { $"{entities.Count()} entities" },
            cancellationToken: cancellationToken);
    }

    #endregion

    #region Private Helper Methods

    private Dictionary<string, object> CreatePayload(TEntity entity, bool isCreateOperation)
    {
        var payload = new Dictionary<string, object>();
        var properties = entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (!property.CanRead || !property.CanWrite) continue;
            if (property.GetCustomAttribute<NotMappedAttribute>() is not null) continue;
            if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null) continue;

            var attribute = property.GetCustomAttribute<ODataFieldAttribute>();

            if (isCreateOperation && attribute?.IgnoreOnCreate == true) continue;
            if (!isCreateOperation && attribute?.IgnoreOnUpdate == true) continue;

            var value = property.GetValue(entity);
            var defaultValue = property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null;

            if (value is not null && !value.Equals(defaultValue))
            {
                var propertyName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
                payload.Add(propertyName, value);
            }
        }
        return payload;
    }

    #endregion
}

/// <summary>
/// Centralized exception handling for OData operations with comprehensive logging and metrics.
/// Implements DRY principle by handling all exception types in one place.
/// </summary>
internal class ODataExceptionHandler<TEntity> where TEntity : class, IEntity
{
    private readonly ILogger _logger;
    private readonly string _entityTypeName;

    public ODataExceptionHandler(ILogger logger)
    {
        _logger = logger;
        _entityTypeName = typeof(TEntity).Name;
    }

    /// <summary>
    /// Executes an operation that returns a single entity with comprehensive exception handling.
    /// </summary>
    public async Task<Result<TEntity>> ExecuteAsync(
        string operationName,
        Func<Task<TEntity>> operation,
        Func<object[]>? entityKey = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var context = new OperationContext(operationName, _entityTypeName, entityKey?.Invoke());

        try
        {
            var result = await operation();

            stopwatch.Stop();
            LogSuccess(context, stopwatch.Elapsed);

            return Result<TEntity>.Ok(result);
        }
        catch (ODataNotFoundException ex)
        {
            stopwatch.Stop();
            return HandleNotFound<TEntity>(context, stopwatch.Elapsed, ex);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HandleException<TEntity>(context, stopwatch.Elapsed, ex, cancellationToken);
        }
    }

    /// <summary>
    /// Executes an operation that returns a collection of entities.
    /// </summary>
    public async Task<Result<IEnumerable<TEntity>>> ExecuteCollectionAsync(
        string operationName,
        Func<Task<IEnumerable<TEntity>>> operation,
        Func<object[]>? entityKey = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var context = new OperationContext(operationName, _entityTypeName, entityKey?.Invoke());

        try
        {
            var result = await operation();
            var resultList = result as IList<TEntity> ?? result.ToList();

            stopwatch.Stop();
            LogSuccess(context, stopwatch.Elapsed, resultList.Count);

            return Result<IEnumerable<TEntity>>.Ok(resultList);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HandleException<IEnumerable<TEntity>>(context, stopwatch.Elapsed, ex, cancellationToken);
        }
    }

    /// <summary>
    /// Executes an operation that returns a scalar value.
    /// </summary>
    public async Task<Result<T>> ExecuteScalarAsync<T>(
        string operationName,
        Func<Task<T>> operation,
        Func<object[]>? entityKey = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var context = new OperationContext(operationName, _entityTypeName, entityKey?.Invoke());

        try
        {
            var result = await operation();

            stopwatch.Stop();
            LogSuccess(context, stopwatch.Elapsed);

            return Result<T>.Ok(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HandleException<T>(context, stopwatch.Elapsed, ex, cancellationToken);
        }
    }

    /// <summary>
    /// Executes an operation that doesn't return a value (void operations).
    /// </summary>
    public async Task<Result> ExecuteNonQueryAsync(
        string operationName,
        Func<Task> operation,
        Func<object[]>? entityKey = null,
        CancellationToken cancellationToken = default,
        bool treatNotFoundAsSuccess = false)
    {
        var stopwatch = Stopwatch.StartNew();
        var context = new OperationContext(operationName, _entityTypeName, entityKey?.Invoke());

        try
        {
            await operation();

            stopwatch.Stop();
            LogSuccess(context, stopwatch.Elapsed);

            return Result.Ok();
        }
        catch (WebRequestException ex) when (treatNotFoundAsSuccess && ex.Code == HttpStatusCode.NotFound)
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "{Operation} on {EntityType} - entity not found (treating as success). Duration: {ElapsedMs}ms",
                context.OperationName, context.EntityType, stopwatch.ElapsedMilliseconds);

            return Result.Ok(); // Idempotent operation
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HandleException(context, stopwatch.Elapsed, ex, cancellationToken);
        }
    }

    #region Exception Handling

    private Result<T> HandleException<T>(
        OperationContext context,
        TimeSpan elapsed,
        Exception exception,
        CancellationToken cancellationToken)
    {
        return exception switch
        {
            WebRequestException webEx => HandleWebRequestException<T>(context, elapsed, webEx),
            TaskCanceledException tcEx when !cancellationToken.IsCancellationRequested => HandleTimeout<T>(context, elapsed, tcEx),
            OperationCanceledException ocEx => HandleCancellation<T>(context, elapsed, ocEx),
            _ => HandleUnexpectedException<T>(context, elapsed, exception)
        };
    }

    private Result HandleException(
        OperationContext context,
        TimeSpan elapsed,
        Exception exception,
        CancellationToken cancellationToken)
    {
        return exception switch
        {
            WebRequestException webEx => HandleWebRequestException(context, elapsed, webEx),
            TaskCanceledException tcEx when !cancellationToken.IsCancellationRequested => HandleTimeout(context, elapsed, tcEx),
            OperationCanceledException ocEx => HandleCancellation(context, elapsed, ocEx),
            _ => HandleUnexpectedException(context, elapsed, exception)
        };
    }

    private Result<T> HandleWebRequestException<T>(OperationContext context, TimeSpan elapsed, WebRequestException exception)
    {
        var (errorCode, errorMessage, errorType) = exception.Code switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                ("Unauthorized", $"Authentication or authorization failed: {exception.Message}", ErrorType.Failure),

            HttpStatusCode.BadRequest =>
                ("ValidationFailed", $"Request validation failed: {exception.Message}", ErrorType.Validation),

            HttpStatusCode.NotFound =>
                ("NotFound", "Entity was not found", ErrorType.NotFound),

            HttpStatusCode.Conflict =>
                ("Conflict", $"Entity already exists or conflict occurred: {exception.Message}", ErrorType.Conflict),

            HttpStatusCode.PreconditionFailed =>
                ("ConcurrencyConflict", "Entity has been modified by another user", ErrorType.Conflict),

            HttpStatusCode.TooManyRequests =>
                ("RateLimitExceeded", "OData service rate limit exceeded", ErrorType.Failure),

            _ => ($"{context.OperationName}Failed", $"OData operation failed: {exception.Message}", ErrorType.Failure)
        };

        var logLevel = errorType == ErrorType.Validation ? LogLevel.Warning : LogLevel.Error;

        _logger.Log(logLevel,
            exception,
            "{Operation} on {EntityType} failed after {ElapsedMs}ms. StatusCode: {StatusCode}, Error: {Error}",
            context.OperationName, context.EntityType, elapsed.TotalMilliseconds, exception.Code, errorMessage);

        return Result<T>.Fail(new Error($"{context.EntityType}.{errorCode}", errorMessage, errorType, exception));
    }

    private Result HandleWebRequestException(OperationContext context, TimeSpan elapsed, WebRequestException exception)
    {
        var result = HandleWebRequestException<object>(context, elapsed, exception);
        return Result.Fail(result.Error!);
    }

    private Result<T> HandleNotFound<T>(OperationContext context, TimeSpan elapsed, ODataNotFoundException exception)
    {
        _logger.LogInformation(
            "{Operation} on {EntityType} - entity not found after {ElapsedMs}ms. Key: {@Key}",
            context.OperationName, context.EntityType, elapsed.TotalMilliseconds, context.EntityKey);

        return Result<T>.Fail(new Error(
            $"{context.EntityType}.NotFound",
            exception.Message,
            ErrorType.NotFound,
            exception));
    }

    private Result<T> HandleTimeout<T>(OperationContext context, TimeSpan elapsed, TaskCanceledException exception)
    {
        _logger.LogError(exception,
            "{Operation} on {EntityType} timed out after {ElapsedMs}ms",
            context.OperationName, context.EntityType, elapsed.TotalMilliseconds);

        return Result<T>.Fail(new Error(
            $"{context.EntityType}.Timeout",
            "Request timed out",
            ErrorType.Failure,
            exception));
    }

    private Result HandleTimeout(OperationContext context, TimeSpan elapsed, TaskCanceledException exception)
    {
        var result = HandleTimeout<object>(context, elapsed, exception);
        return Result.Fail(result.Error!);
    }

    private Result<T> HandleCancellation<T>(OperationContext context, TimeSpan elapsed, OperationCanceledException exception)
    {
        _logger.LogInformation(
            "{Operation} on {EntityType} was cancelled after {ElapsedMs}ms",
            context.OperationName, context.EntityType, elapsed.TotalMilliseconds);

        return Result<T>.Fail(new Error(
            $"{context.EntityType}.Cancelled",
            "Operation was cancelled",
            ErrorType.Failure,
            exception));
    }

    private Result HandleCancellation(OperationContext context, TimeSpan elapsed, OperationCanceledException exception)
    {
        var result = HandleCancellation<object>(context, elapsed, exception);
        return Result.Fail(result.Error!);
    }

    private Result<T> HandleUnexpectedException<T>(OperationContext context, TimeSpan elapsed, Exception exception)
    {
        _logger.LogError(exception,
            "{Operation} on {EntityType} failed with unexpected exception after {ElapsedMs}ms: {ExceptionType}",
            context.OperationName, context.EntityType, elapsed.TotalMilliseconds, exception.GetType().Name);

        return Result<T>.Fail(new Error(
            $"{context.EntityType}.UnexpectedError",
            $"An unexpected error occurred: {exception.Message}",
            ErrorType.Failure,
            exception));
    }

    private Result HandleUnexpectedException(OperationContext context, TimeSpan elapsed, Exception exception)
    {
        var result = HandleUnexpectedException<object>(context, elapsed, exception);
        return Result.Fail(result.Error!);
    }

    #endregion

    #region Logging

    private void LogSuccess(OperationContext context, TimeSpan elapsed, int? count = null)
    {
        if (count.HasValue)
        {
            _logger.LogInformation(
                "{Operation} on {EntityType} succeeded in {ElapsedMs}ms. Retrieved {Count} entities",
                context.OperationName, context.EntityType, elapsed.TotalMilliseconds, count.Value);
        }
        else
        {
            _logger.LogInformation(
                "{Operation} on {EntityType} succeeded in {ElapsedMs}ms",
                context.OperationName, context.EntityType, elapsed.TotalMilliseconds);
        }
    }

    #endregion
}

/// <summary>
/// Context information for OData operations, used for structured logging.
/// </summary>
internal record OperationContext(string OperationName, string EntityType, object[]? EntityKey = null);

/// <summary>
/// Custom exception for OData NotFound scenarios to distinguish from other errors.
/// </summary>
internal class ODataNotFoundException : Exception
{
    public ODataNotFoundException(string message) : base(message) { }
}