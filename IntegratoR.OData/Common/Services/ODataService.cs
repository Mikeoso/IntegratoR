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
using Polly.Retry;
using Simple.OData.Client;

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

    private readonly IODataClient _client;
    private readonly ILogger<ODataService<TEntity>> _logger;
    private readonly ODataExceptionHandler<TEntity> _exceptionHandler;

    public ODataService(
        IODataClient client,
        ILogger<ODataService<TEntity>> logger,
        AsyncRetryPolicy? retryPolicy = null)
    {
        _client = client;
        _logger = logger;
        _exceptionHandler = new ODataExceptionHandler<TEntity>(logger, retryPolicy);
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
                    .For<TEntity>()
                    .Set(payload)
                    .InsertEntryAsync(true, cancellationToken)
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
                var query = _client.For<TEntity>();

                if (filter is not null)
                {
                    query = query.Filter(filter);
                    _logger.LogDebug("Executing FindAsync for {EntityType} with filter: {Filter}",
                        typeof(TEntity).Name, filter.ToString());
                }

                return await query.FindEntriesAsync(cancellationToken).ConfigureAwait(false);
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

                var entity = await _client
                    .For<TEntity>()
                    .Key(keyValues)
                    .FindEntryAsync(cancellationToken)
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

                return await _client
                    .For<TEntity>()
                    .Key(entity)
                    .Set(entity)
                    .UpdateEntryAsync(cancellationToken)
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

                await _client
                    .For<TEntity>()
                    .Key(entity)
                    .DeleteEntryAsync(cancellationToken)
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
            operation: async () =>
            {
                var query = _client.For<TEntity>();

                if (filter is not null) query = query.Filter(filter);
                if (expand is not null) query = query.Expand(expand);
                if (select is not null) query = query.Select(select);
                if (skip.HasValue) query = query.Skip(skip.Value);
                if (top.HasValue) query = query.Top(top.Value);

                return await query.FindEntriesAsync(cancellationToken).ConfigureAwait(false);
            },
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<IEnumerable<TEntity>>> FindAll(CancellationToken cancellationToken = default)
    {
        return _exceptionHandler.ExecuteCollectionAsync(
            operationName: "FindAll",
            operation: async () => await _client
                .For<TEntity>()
                .FindEntriesAsync(cancellationToken)
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
            operation: async () =>
            {
                var query = _client.For<TEntity>();
                if (filter is not null) query = query.Filter(filter);
                return await query.Count().FindScalarAsync<int>(cancellationToken).ConfigureAwait(false);
            },
            cancellationToken: cancellationToken);
    }

    #endregion

    #region IODataBatchService Implementation

    /// <inheritdoc />
    public Task<Result> AddBatchAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        return _exceptionHandler.ExecuteNonQueryAsync(
            operationName: "AddBatch",
            operation: async () =>
            {
                var batch = new ODataBatch(_client);
                foreach (var entity in entities)
                {
                    batch += c => c.For<TEntity>()
                        .Set(entity)
                        .InsertEntryAsync(cancellationToken);
                }
                await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            },
            entityKey: () => new object[] { $"{entities.Count()} entities" },
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> DeleteBatchAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        return _exceptionHandler.ExecuteNonQueryAsync(
            operationName: "DeleteBatch",
            operation: async () =>
            {
                var batch = new ODataBatch(_client);
                foreach (var entity in entities)
                {
                    batch += c => c.For<TEntity>()
                        .Key(entity.GetCompositeKey())
                        .DeleteEntryAsync(cancellationToken);
                }
                await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            },
            entityKey: () => new object[] { $"{entities.Count()} entities" },
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> UpdateBatchAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        return _exceptionHandler.ExecuteNonQueryAsync(
            operationName: "UpdateBatch",
            operation: async () =>
            {
                var batch = new ODataBatch(_client);
                foreach (var entity in entities)
                {
                    batch += c => c.For<TEntity>()
                        .Key(entity.GetCompositeKey())
                        .Set(entity)
                        .UpdateEntryAsync(cancellationToken);
                }
                await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            },
            entityKey: () => new object[] { $"{entities.Count()} entities" },
            cancellationToken: cancellationToken);
    }

    #endregion

    #region Helper Methods

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
