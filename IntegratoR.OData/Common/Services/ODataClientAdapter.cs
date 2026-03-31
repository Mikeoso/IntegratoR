using System.Linq.Expressions;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.OData.Domain.Models;
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
        if (payload is IDictionary<string, object> dict)
        {
            var jsonResult = await _client.CreateAsync(entitySet, dict, null, cancellationToken)
                .ConfigureAwait(false);

            return DeserializeResponse<TEntity>(jsonResult);
        }

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
        object payload,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        if (payload is IDictionary<string, object> dict)
        {
            var jsonResult = await _client.UpdateAsync<object, object>(entitySet, key, dict, null, cancellationToken)
                .ConfigureAwait(false);

            return DeserializeResponse<TEntity>(jsonResult);
        }

        return await _client.UpdateAsync<TEntity>(entitySet, key, (TEntity)payload, null, cancellationToken)
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
    public async Task<IReadOnlyList<BatchOperationResult>> BatchCreateAsync(
        string entitySet,
        IEnumerable<IDictionary<string, object>> payloads,
        CancellationToken cancellationToken = default)
    {
        var batch = _client.CreateBatch();
        batch.Changeset(changeset =>
        {
            foreach (var payload in payloads)
            {
                changeset.Create(entitySet, payload);
            }
        });
        ODataBatchResponse response = await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return MapBatchResponse(response);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BatchOperationResult>> BatchUpdateAsync(
        string entitySet,
        IEnumerable<(object Key, IDictionary<string, object> Payload)> items,
        CancellationToken cancellationToken = default)
    {
        var batch = _client.CreateBatch();
        batch.Changeset(changeset =>
        {
            foreach (var (key, payload) in items)
            {
                changeset.Update<object, object>(entitySet, key, payload);
            }
        });
        ODataBatchResponse response = await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return MapBatchResponse(response);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BatchOperationResult>> BatchDeleteAsync(
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
        ODataBatchResponse response = await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return MapBatchResponse(response);
    }

    /// <summary>
    /// Round-trips a PanoramicData response object through JSON to produce a strongly-typed entity.
    /// Required because dictionary-based payloads cause PanoramicData to return <c>Dictionary&lt;string, object&gt;</c>
    /// instead of <typeparamref name="TEntity"/>.
    /// </summary>
    private static TEntity DeserializeResponse<TEntity>(object response) where TEntity : class
    {
        var json = System.Text.Json.JsonSerializer.Serialize(response);
        return System.Text.Json.JsonSerializer.Deserialize<TEntity>(json, CaseInsensitiveOptions)!;
    }

    private static IReadOnlyList<BatchOperationResult> MapBatchResponse(ODataBatchResponse response)
    {
        var results = new List<BatchOperationResult>();
        var index = 0;

        foreach (var result in response.Results)
        {
            results.Add(new BatchOperationResult
            {
                Index = index++,
                StatusCode = result.StatusCode,
                IsSuccess = result.IsSuccess,
                ErrorMessage = result.ErrorMessage,
                ResponseBody = result.ResponseBody
            });
        }

        return results;
    }
}
