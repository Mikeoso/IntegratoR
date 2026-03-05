using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.OData.Common.Annotations;
using IntegratoR.OData.Interfaces.Services;
using Microsoft.Extensions.Logging;
using PanoramicData.OData.Client.Exceptions;
using Polly.Retry;

namespace IntegratoR.OData.Common.Services;

/// <summary>
/// Generic service implementation for OData operations with comprehensive error handling,
/// automatic retry policies, and performance tracking.
/// </summary>
/// <typeparam name="TEntity">The entity type that implements <see cref="IEntity"/>.</typeparam>
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

        return _exceptionHandler.ExecuteAsync(
            operationName: "GetByKey",
            operation: async () =>
            {
                _logger.LogDebug("Retrieving {EntityType} by key: {@KeyValues}",
                    typeof(TEntity).Name, keyValues);

                var key = BuildCompositeKeyObject(keyValues);

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

        return _exceptionHandler.ExecuteAsync(
            operationName: "Update",
            operation: async () =>
            {
                _logger.LogDebug("Updating {EntityType} with key {@Key}",
                    typeof(TEntity).Name, entity.GetCompositeKey());

                var key = BuildCompositeKeyObject(entity.GetCompositeKey());

                return await _client
                    .UpdateAsync<TEntity>(_entitySetName, key, entity, cancellationToken)
                    .ConfigureAwait(false);
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

        return _exceptionHandler.ExecuteNonQueryAsync(
            operationName: "Delete",
            operation: async () =>
            {
                _logger.LogDebug("Deleting {EntityType} with key {@Key}",
                    typeof(TEntity).Name, entity.GetCompositeKey());

                var key = BuildCompositeKeyObject(entity.GetCompositeKey());

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
            operation: async () => await _client
                .FindEntriesAsync<TEntity>(_entitySetName, filter, expand, select, skip, top, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<IEnumerable<TEntity>>> FindAll(CancellationToken cancellationToken = default)
    {
        return _exceptionHandler.ExecuteCollectionAsync(
            operationName: "FindAll",
            operation: async () => await _client
                .FindEntriesAsync<TEntity>(_entitySetName, cancellationToken: cancellationToken)
                .ConfigureAwait(false),
            cancellationToken: cancellationToken);
    }

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
                await _client
                    .BatchCreateAsync<TEntity>(_entitySetName, entityList, cancellationToken)
                    .ConfigureAwait(false);
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

        return _exceptionHandler.ExecuteNonQueryAsync(
            operationName: "DeleteBatch",
            operation: async () =>
            {
                var keys = entityList.Select(e => BuildCompositeKeyObject(e.GetCompositeKey()));
                await _client
                    .BatchDeleteAsync(_entitySetName, keys, cancellationToken)
                    .ConfigureAwait(false);
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

        return _exceptionHandler.ExecuteNonQueryAsync(
            operationName: "UpdateBatch",
            operation: async () =>
            {
                var items = entityList.Select(e =>
                    (BuildCompositeKeyObject(e.GetCompositeKey()), e));
                await _client
                    .BatchUpdateAsync<TEntity>(_entitySetName, items, cancellationToken)
                    .ConfigureAwait(false);
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
    /// For single keys, returns the value directly. For composite keys, returns a dictionary.
    /// </summary>
    private object BuildCompositeKeyObject(object[] keyValues)
    {
        if (keyValues.Length == 1)
        {
            return keyValues[0];
        }

        var keyProperties = KeyPropertyCache.GetOrAdd(typeof(TEntity), type =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<System.ComponentModel.DataAnnotations.KeyAttribute>() is not null)
                .ToArray());

        if (keyProperties.Length == keyValues.Length)
        {
            var keyDict = new Dictionary<string, object>();
            for (var i = 0; i < keyProperties.Length; i++)
            {
                var jsonName = keyProperties[i].GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>()?.Name
                    ?? keyProperties[i].Name;
                keyDict[jsonName] = keyValues[i];
            }
            return keyDict;
        }

        // Fallback: return first value if key property count doesn't match
        _logger.LogWarning(
            "Key property count mismatch for {EntityType}: expected {Expected} key properties but received {Actual} values. " +
            "Falling back to first key value. Check [Key] attributes on the entity.",
            typeof(TEntity).Name, keyProperties.Length, keyValues.Length);
        return keyValues[0];
    }

    private static Dictionary<string, object> CreatePayload(TEntity entity, bool isCreateOperation)
    {
        var metadata = PropertyMetadataCache.GetOrAdd(
            entity.GetType(),
            type => BuildPropertyMetadata(type));

        var payload = new Dictionary<string, object>();

        foreach (var prop in metadata)
        {
            if (isCreateOperation && prop.IgnoreOnCreate) continue;
            if (!isCreateOperation && prop.IgnoreOnUpdate) continue;

            var value = prop.Property.GetValue(entity);

            if (value is not null && !value.Equals(prop.DefaultValue))
            {
                payload.Add(prop.PayloadName, value);
            }
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
                var payloadName = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name;
                var defaultValue = p.PropertyType.IsValueType
                    ? Activator.CreateInstance(p.PropertyType)
                    : null;

                return new CachedPropertyMetadata(
                    p,
                    payloadName,
                    odataField?.IgnoreOnCreate ?? false,
                    odataField?.IgnoreOnUpdate ?? false,
                    defaultValue);
            })
            .ToArray();
    }

    private sealed record CachedPropertyMetadata(
        PropertyInfo Property,
        string PayloadName,
        bool IgnoreOnCreate,
        bool IgnoreOnUpdate,
        object? DefaultValue);

    #endregion
}
