using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.OData.Common.Annotations;
using IntegratoR.OData.Common.Exceptions;
using IntegratoR.OData.Domain.Models;
using IntegratoR.OData.Interfaces.Services;
using Microsoft.Extensions.Logging;
using PanoramicData.OData.Client.Exceptions;
using Polly.Retry;

namespace IntegratoR.OData.Common.Services;

/// <summary>
/// Provides generic OData CRUD, query, and batch operations with error handling and optional retry.
/// </summary>
/// <typeparam name="TEntity">The type of the entity, which must implement <see cref="IEntity"/>.</typeparam>
public class ODataService<TEntity> : IODataService<TEntity>, IODataBatchService<TEntity>
    where TEntity : class, IEntity
{
    private static readonly ConcurrentDictionary<Type, CachedPropertyMetadata[]> PropertyMetadataCache = new();
    private static readonly ConcurrentDictionary<Type, string> EntitySetNameCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> KeyPropertyCache = new();

    private readonly IODataClientAdapter _client;
    private readonly ILogger<ODataService<TEntity>> _logger;
    private readonly ODataExceptionHandler<TEntity> _exceptionHandler;
    private readonly string _entitySetName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataService{TEntity}"/> class.
    /// </summary>
    /// <param name="client">The OData client adapter used to issue requests.</param>
    /// <param name="logger">The logger for structured operation logging.</param>
    /// <param name="retryPolicy">An optional Polly retry policy applied to transient failures; when <see langword="null"/>, no retries are performed.</param>
    public ODataService(
        IODataClientAdapter client,
        ILogger<ODataService<TEntity>> logger,
        AsyncRetryPolicy? retryPolicy = null)
    {
        _client = client;
        _logger = logger;
        _exceptionHandler = new ODataExceptionHandler<TEntity>(logger, retryPolicy);
        _entitySetName = ResolveEntitySetName();
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
                _logger.LogDebug("Adding entity {EntityType} with payload: {@Payload}",
                    typeof(TEntity).Name, payload);

                return await _client
                    .CreateAsync<TEntity>(_entitySetName, payload, cancellationToken)
                    .ConfigureAwait(false);
            },
            entityKey: () => entity.GetCompositeKey(),
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<IEnumerable<TEntity>>> FindAsync(
        Expression<Func<TEntity, bool>>? filter,
        CancellationToken cancellationToken = default)
    {
        return _exceptionHandler.ExecuteCollectionAsync(
            operationName: "Find",
            operation: async () =>
            {
                if (filter is not null)
                {
                    _logger.LogDebug("Executing FindAsync for {EntityType} with filter: {Filter}",
                        typeof(TEntity).Name, filter.ToString());
                }

                return await _client
                    .FindEntriesAsync<TEntity>(_entitySetName, filter, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<TEntity>> GetByKeyAsync(object[] keyValues, CancellationToken cancellationToken = default)
    {
        if (keyValues == null || keyValues.Length == 0)
        {
            return Task.FromResult(Result.Fail<TEntity>(new IntegrationError(
                $"{typeof(TEntity).Name}.InvalidKey",
                "Key values cannot be null or empty",
                ErrorType.Validation)));
        }

        var keyResult = BuildCompositeKeyObject(keyValues);
        if (keyResult.IsFailed)
        {
            return Task.FromResult(Result.Fail<TEntity>(keyResult.Errors));
        }

        var key = keyResult.Value;

        return _exceptionHandler.ExecuteAsync(
            operationName: "GetByKey",
            operation: async () =>
            {
                _logger.LogDebug("Retrieving {EntityType} by key: {@KeyValues}",
                    typeof(TEntity).Name, keyValues);

                var entity = await _client
                    .FindByKeyAsync<TEntity>(_entitySetName, key, cancellationToken)
                    .ConfigureAwait(false);

                if (entity is null)
                {
                    throw new ODataNotFoundException(
                        "Entity with the specified composite key was not found");
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
            return Task.FromResult(Result.Fail<TEntity>(new IntegrationError(
                "Validation.NullEntity",
                "The provided entity cannot be null",
                ErrorType.Validation)));
        }

        var keyResult = BuildCompositeKeyObject(entity.GetCompositeKey());
        if (keyResult.IsFailed)
        {
            return Task.FromResult(Result.Fail<TEntity>(keyResult.Errors));
        }

        var key = keyResult.Value;

        return _exceptionHandler.ExecuteAsync(
            operationName: "Update",
            operation: async () =>
            {
                _logger.LogDebug("Updating {EntityType} with key {@Key}",
                    typeof(TEntity).Name, entity.GetCompositeKey());

                var payload = CreatePayload(entity, isCreateOperation: false);

                TEntity? updated = await _client
                    .UpdateAsync<TEntity>(_entitySetName, key, payload, cancellationToken)
                    .ConfigureAwait(false);

                // A composite-key PATCH may return 204 No Content (D365 does not echo the entity), so
                // the adapter yields null. The update succeeded — return the caller's entity so a
                // successful Result<TEntity> always carries a non-null value rather than tripping
                // consumers on a null Value.
                return updated ?? entity;
            },
            entityKey: () => entity.GetCompositeKey(),
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity == null)
        {
            return Task.FromResult(Result.Fail(new IntegrationError(
                "Validation.NullEntity",
                "The provided entity cannot be null",
                ErrorType.Validation)));
        }

        var keyResult = BuildCompositeKeyObject(entity.GetCompositeKey());
        if (keyResult.IsFailed)
        {
            return Task.FromResult(Result.Fail(keyResult.Errors));
        }

        var key = keyResult.Value;

        return _exceptionHandler.ExecuteNonQueryAsync(
            operationName: "Delete",
            operation: async () =>
            {
                _logger.LogDebug("Deleting {EntityType} with key {@Key}",
                    typeof(TEntity).Name, entity.GetCompositeKey());

                await _client
                    .DeleteAsync(_entitySetName, key, cancellationToken)
                    .ConfigureAwait(false);
            },
            entityKey: () => entity.GetCompositeKey(),
            cancellationToken: cancellationToken,
            treatNotFoundAsSuccess: true);
    }

    #endregion

    #region IODataService Implementation

    /// <inheritdoc />
    [Obsolete("Since v1.4.0; the Func-based orderBy was never applied to the OData query (it was silently dropped). Use the overload taking IReadOnlyList<(Expression<Func<TEntity, object>> KeySelector, bool Descending)> orderBy.")]
    public Task<Result<IEnumerable<TEntity>>> QueryAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Expression<Func<TEntity, object>>? expand = null,
        Expression<Func<TEntity, object>>? select = null,
        int? skip = null,
        int? top = null,
        CancellationToken cancellationToken = default)
    {
        // orderBy is intentionally NOT forwarded — the Func-based shape cannot be translated to an
        // OData $orderby clause and was always silently dropped. Use the typed overload below.
        return _exceptionHandler.ExecuteCollectionAsync(
            operationName: "Query",
            operation: async () => await _client
                .FindEntriesAsync<TEntity>(_entitySetName, filter, expand, select, orderBy: null, skip, top, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<IEnumerable<TEntity>>> QueryAsync(
        Expression<Func<TEntity, bool>>? filter,
        IReadOnlyList<(Expression<Func<TEntity, object>> KeySelector, bool Descending)>? orderBy,
        Expression<Func<TEntity, object>>? expand = null,
        Expression<Func<TEntity, object>>? select = null,
        int? skip = null,
        int? top = null,
        CancellationToken cancellationToken = default)
    {
        return _exceptionHandler.ExecuteCollectionAsync(
            operationName: "Query",
            operation: async () => await _client
                .FindEntriesAsync<TEntity>(_entitySetName, filter, expand, select, orderBy, skip, top, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<IEnumerable<TEntity>>> FindAllAsync(CancellationToken cancellationToken = default)
    {
        return _exceptionHandler.ExecuteCollectionAsync(
            operationName: "FindAll",
            operation: async () => await _client
                .FindEntriesAsync<TEntity>(_entitySetName, cancellationToken: cancellationToken)
                .ConfigureAwait(false),
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    [Obsolete("since v1.4.0; use FindAllAsync; removed next MAJOR")]
    public Task<Result<IEnumerable<TEntity>>> FindAll(CancellationToken cancellationToken = default)
        => FindAllAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Result<int>> CountAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        return _exceptionHandler.ExecuteScalarAsync(
            operationName: "Count",
            operation: async () => await _client
                .CountAsync<TEntity>(_entitySetName, filter, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken: cancellationToken);
    }

    #endregion

    #region IODataBatchService Implementation

    /// <inheritdoc />
    public Task<Result> AddBatchAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        var entityList = entities as IList<TEntity> ?? entities.ToList();

        return _exceptionHandler.ExecuteNonQueryAsync(
            operationName: "AddBatch",
            operation: async () =>
            {
                var payloads = entityList.Select(e => CreatePayload(e, isCreateOperation: true)).ToList();
                IReadOnlyList<BatchOperationResult> results = await _client
                    .BatchCreateAsync(_entitySetName, payloads, cancellationToken)
                    .ConfigureAwait(false);
                return BuildBatchErrors(results, "AddBatch", entityList);
            },
            entityKey: () => new object[] { $"{entityList.Count} entities" },
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> DeleteBatchAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        var entityList = entities as IList<TEntity> ?? entities.ToList();

        var keysResult = BuildBatchKeys(entityList);
        if (keysResult.IsFailed)
        {
            return Task.FromResult(Result.Fail(keysResult.Errors));
        }

        var keys = keysResult.Value;

        return _exceptionHandler.ExecuteNonQueryAsync(
            operationName: "DeleteBatch",
            operation: async () =>
            {
                IReadOnlyList<BatchOperationResult> results = await _client
                    .BatchDeleteAsync(_entitySetName, keys, cancellationToken)
                    .ConfigureAwait(false);
                return BuildBatchErrors(results, "DeleteBatch", entityList);
            },
            entityKey: () => new object[] { $"{entityList.Count} entities" },
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> UpdateBatchAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        var entityList = entities as IList<TEntity> ?? entities.ToList();

        var keysResult = BuildBatchKeys(entityList);
        if (keysResult.IsFailed)
        {
            return Task.FromResult(Result.Fail(keysResult.Errors));
        }

        var items = entityList
            .Select((e, i) => (keysResult.Value[i], (IDictionary<string, object>)CreatePayload(e, isCreateOperation: false)))
            .ToList();

        return _exceptionHandler.ExecuteNonQueryAsync(
            operationName: "UpdateBatch",
            operation: async () =>
            {
                IReadOnlyList<BatchOperationResult> results = await _client
                    .BatchUpdateAsync(_entitySetName, items, cancellationToken)
                    .ConfigureAwait(false);
                return BuildBatchErrors(results, "UpdateBatch", entityList);
            },
            entityKey: () => new object[] { $"{entityList.Count} entities" },
            cancellationToken: cancellationToken);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Resolves the OData entity set name from the <see cref="TableAttribute"/> on the entity type.
    /// Falls back to the pluralised type name if the attribute is not present.
    /// </summary>
    private static string ResolveEntitySetName()
    {
        return EntitySetNameCache.GetOrAdd(typeof(TEntity), type =>
        {
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            return tableAttr?.Name ?? $"{type.Name}s";
        });
    }

    /// <summary>
    /// Builds a composite key object from key values by mapping them to key property names.
    /// For single keys, returns the value directly. For composite keys, returns a dictionary of
    /// <c>wireName → value</c>. Returns a <see cref="ErrorType.Validation"/> failure when the
    /// supplied value count does not match the number of <c>[Key]</c> properties, or when any key
    /// element is null (a null key cannot identify an entity).
    /// </summary>
    private static Result<object> BuildCompositeKeyObject(object[] keyValues)
    {
        if (keyValues.Length == 1)
        {
            if (keyValues[0] is null)
            {
                return Result.Fail<object>(new IntegrationError(
                    $"{typeof(TEntity).Name}.InvalidKey",
                    "The single key value was null; a null key cannot identify an entity.",
                    ErrorType.Validation));
            }

            return Result.Ok(keyValues[0]);
        }

        // GetProperties() order is undefined, so sort by MetadataToken (declaration order within
        // the type) to keep each [Key] property zipped to its corresponding value deterministically.
        var keyProperties = KeyPropertyCache.GetOrAdd(typeof(TEntity), type =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<System.ComponentModel.DataAnnotations.KeyAttribute>() is not null)
                .OrderBy(p => p.MetadataToken)
                .ToArray());

        if (keyProperties.Length != keyValues.Length)
        {
            return Result.Fail<object>(new IntegrationError(
                $"{typeof(TEntity).Name}.KeyCountMismatch",
                $"Expected {keyProperties.Length} key properties but received {keyValues.Length} values. " +
                $"Check [Key] attributes on {typeof(TEntity).Name}.",
                ErrorType.Validation));
        }

        var keyDict = new Dictionary<string, object>();
        for (var i = 0; i < keyProperties.Length; i++)
        {
            var name = PropertyNameResolver.Resolve(keyProperties[i]);
            if (keyValues[i] is null)
            {
                return Result.Fail<object>(new IntegrationError(
                    $"{typeof(TEntity).Name}.InvalidKey",
                    $"Composite key element at index {i} for field '{name}' was null; a null key cannot identify an entity.",
                    ErrorType.Validation));
            }

            keyDict[name] = keyValues[i];
        }

        return Result.Ok<object>(keyDict);
    }

    /// <summary>
    /// Builds the composite key object for every entity in a batch, short-circuiting on the first
    /// <see cref="ErrorType.Validation"/> failure so a bad key is surfaced before any HTTP traffic.
    /// </summary>
    private static Result<List<object>> BuildBatchKeys(IList<TEntity> entities)
    {
        var keys = new List<object>(entities.Count);
        foreach (var entity in entities)
        {
            var keyResult = BuildCompositeKeyObject(entity.GetCompositeKey());
            if (keyResult.IsFailed)
            {
                return Result.Fail<List<object>>(keyResult.Errors);
            }

            keys.Add(keyResult.Value);
        }

        return Result.Ok(keys);
    }

    private static Dictionary<string, object> CreatePayload(TEntity entity, bool isCreateOperation)
    {
        var metadata = PropertyMetadataCache.GetOrAdd(
            entity.GetType(),
            type => BuildPropertyMetadata(type));

        var payload = new Dictionary<string, object>();
        List<string>? missingRequired = null;

        foreach (var prop in metadata)
        {
            if (isCreateOperation && prop.IgnoreOnCreate) continue;
            if (!isCreateOperation && prop.IgnoreOnUpdate) continue;

            var value = prop.Property.GetValue(entity);

            if (value is null)
            {
                // Required fields with null value on create are validation errors
                if (prop.IsRequired && isCreateOperation)
                {
                    missingRequired ??= [];
                    missingRequired.Add(prop.PayloadName);
                }
                // Non-required null values are simply omitted
                continue;
            }

            // Include required fields even when they hold a default value (0, false, etc.)
            if (prop.IsRequired || !value.Equals(prop.DefaultValue))
            {
                payload.Add(prop.PayloadName, value);
            }
        }

        if (missingRequired is { Count: > 0 })
        {
            throw new ODataPayloadValidationException(
                $"Required field(s) missing on {typeof(TEntity).Name}: {string.Join(", ", missingRequired)}",
                missingRequired);
        }

        return payload;
    }

    private static CachedPropertyMetadata[] BuildPropertyMetadata(Type type)
    {
        return type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Where(p => p.GetCustomAttribute<NotMappedAttribute>() is null)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            .Select(p =>
            {
                var odataField = p.GetCustomAttribute<ODataFieldAttribute>();
                var payloadName = PropertyNameResolver.Resolve(p);
                var defaultValue = p.PropertyType.IsValueType
                    ? Activator.CreateInstance(p.PropertyType)
                    : null;

                return new CachedPropertyMetadata(
                    p,
                    payloadName,
                    odataField?.EffectiveIgnoreOnCreate ?? false,
                    odataField?.EffectiveIgnoreOnUpdate ?? false,
                    odataField?.IsRequired ?? false,
                    defaultValue);
            })
            .ToArray();
    }

    /// <summary>
    /// Inspects batch operation results and returns per-entity errors for any failures.
    /// Returns <c>null</c> when all operations succeeded.
    /// </summary>
    private List<IError>? BuildBatchErrors(
        IReadOnlyList<BatchOperationResult> results,
        string operationName,
        IList<TEntity> entities)
    {
        var failures = results.Where(r => !r.IsSuccess).ToList();

        if (failures.Count == 0)
        {
            return null;
        }

        var errors = new List<IError>();

        errors.Add(new IntegrationError(
            $"{typeof(TEntity).Name}.BatchFailed",
            $"{failures.Count} of {results.Count} {operationName} operations failed for {typeof(TEntity).Name}",
            ErrorType.Failure));

        foreach (var failure in failures)
        {
            var innerError = ODataExceptionHandler<TEntity>.ExtractD365InnerError(failure.ResponseBody);

            _logger.LogWarning(
                "{Operation} on {EntityType} - entity at index {Index} failed. " +
                "StatusCode: {StatusCode}, Error: {ErrorMessage}",
                operationName, typeof(TEntity).Name, failure.Index,
                failure.StatusCode, innerError ?? failure.ErrorMessage);

            var errorMessage = innerError ?? failure.ErrorMessage
                ?? $"Operation at index {failure.Index} failed with status {failure.StatusCode}";

            errors.Add(new IntegrationError(
                $"{typeof(TEntity).Name}.BatchOperationFailed",
                $"[Index {failure.Index}, HTTP {failure.StatusCode}] {errorMessage}",
                ErrorType.Failure));
        }

        return errors;
    }

    private sealed record CachedPropertyMetadata(
        PropertyInfo Property,
        string PayloadName,
        bool IgnoreOnCreate,
        bool IgnoreOnUpdate,
        bool IsRequired,
        object? DefaultValue);

    #endregion
}
