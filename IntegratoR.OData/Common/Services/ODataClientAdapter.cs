using System.Linq.Expressions;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.OData.Interfaces.Services;
using PanoramicData.OData.Client;

namespace IntegratoR.OData.Common.Services;

/// <summary>
/// Wraps PanoramicData's <see cref="ODataClient"/> to implement <see cref="IODataClientAdapter"/>,
/// providing a mockable abstraction for unit testing.
/// </summary>
public class ODataClientAdapter : IODataClientAdapter
{
    private static readonly System.Text.Json.JsonSerializerOptions CaseInsensitiveOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly ODataClient _client;

    public ODataClientAdapter(ODataClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public async Task<TEntity> CreateAsync<TEntity>(
        string entitySet,
        object payload,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        // PanoramicData's CreateAsync<T> requires T for both request serialization and response
        // deserialization. We pass a Dictionary<string, object> payload to control which fields
        // are sent (respecting ODataFieldAttribute). Serialize via CreateAsync<Dictionary<string, object>>
        // and manually deserialize the response as TEntity.
        if (payload is IDictionary<string, object> dict)
        {
            var jsonResult = await _client.CreateAsync(entitySet, dict, null, cancellationToken)
                .ConfigureAwait(false);

            // PanoramicData returns the created entity as the same type — deserialize from the response
            var json = System.Text.Json.JsonSerializer.Serialize(jsonResult);
            return System.Text.Json.JsonSerializer.Deserialize<TEntity>(json, CaseInsensitiveOptions)!;
        }

        // If the payload is already a TEntity, use direct serialization
        return await _client.CreateAsync(entitySet, (TEntity)payload, null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TEntity?> FindByKeyAsync<TEntity>(
        string entitySet,
        object key,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var query = _client.For<TEntity>(entitySet).Key(key);
        return await _client.GetFirstOrDefaultAsync(query, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> FindEntriesAsync<TEntity>(
        string entitySet,
        Expression<Func<TEntity, bool>>? filter = null,
        Expression<Func<TEntity, object>>? expand = null,
        Expression<Func<TEntity, object>>? select = null,
        int? skip = null,
        int? top = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var query = _client.For<TEntity>(entitySet);

        if (filter is not null) query = query.Filter(filter);
        if (expand is not null) query = query.Expand(expand);
        if (select is not null) query = query.Select(select);
        if (skip.HasValue) query = query.Skip(skip.Value);
        if (top.HasValue) query = query.Top(top.Value);

        var response = await _client.GetAsync(query, cancellationToken).ConfigureAwait(false);
        return response.Value;
    }

    /// <inheritdoc />
    public async Task<TEntity> UpdateAsync<TEntity>(
        string entitySet,
        object key,
        TEntity entity,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return await _client.UpdateAsync<TEntity>(entitySet, key, entity, null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        string entitySet,
        object key,
        CancellationToken cancellationToken = default)
    {
        await _client.DeleteAsync(entitySet, key, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync<TEntity>(
        string entitySet,
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var query = _client.For<TEntity>(entitySet);
        if (filter is not null) query = query.Filter(filter);

        var count = await _client.GetCountAsync(query, cancellationToken).ConfigureAwait(false);
        return (int)count;
    }

    /// <inheritdoc />
    public async Task BatchCreateAsync<TEntity>(
        string entitySet,
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var batch = _client.CreateBatch();
        batch.Changeset(changeset =>
        {
            foreach (var entity in entities)
            {
                changeset.Create(entitySet, entity);
            }
        });
        await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task BatchUpdateAsync<TEntity>(
        string entitySet,
        IEnumerable<(object Key, TEntity Entity)> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var batch = _client.CreateBatch();
        batch.Changeset(changeset =>
        {
            foreach (var (key, entity) in entities)
            {
                // Use the non-generic overload: Update(string, object key, object patchValues, string? etag)
                changeset.Update<object, object>(entitySet, key, entity);
            }
        });
        await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task BatchDeleteAsync(
        string entitySet,
        IEnumerable<object> keys,
        CancellationToken cancellationToken = default)
    {
        var batch = _client.CreateBatch();
        batch.Changeset(changeset =>
        {
            foreach (var key in keys)
            {
                changeset.Delete(entitySet, key);
            }
        });
        await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }
}
