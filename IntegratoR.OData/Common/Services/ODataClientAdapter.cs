using System.Linq.Expressions;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.OData.Common.Filters;
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
    // Exposed as internal for regression tests in IntegratoR.OData.Tests (see InternalsVisibleTo).
    internal static readonly System.Text.Json.JsonSerializerOptions CaseInsensitiveOptions =
        new()
        {
            PropertyNameCaseInsensitive = true,
            // D365 F&O OData v4 serialises enum values as string names (e.g. "PostingLayer":
            // "Current"). STJ's default enum converter only handles numeric values, so without
            // this converter every CreateCommand/UpdateCommand against an entity with an enum
            // property would throw on the round-trip deserialisation in DeserializeResponse.
            // Registering the converter once here covers every enum in every F&O entity going
            // through the dictionary-payload path.
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

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
        // PanoramicData's IBoundClient.Key only exposes Key(object) — there is no
        // Key(IDictionary<string, object>) or Key(params object[]) overload that the C# compiler
        // can bind to. Passing a composite-key dictionary through Key(object) calls .ToString()
        // on the dict and produces a malformed OData key like
        // "EntitySet(System.Collections.Generic.Dictionary`2[...])". Bypass the broken Key API
        // entirely for composite keys: build an OData $filter with eq predicates on each key
        // field and rely on GetFirstOrDefaultAsync. A composite key uniquely identifies the
        // entity by definition, so the filter returns exactly one row. The literal formatter is
        // reused from IntegratoRODataExpressionTranslator so the wire shape stays in lockstep
        // with filter/select/expand translations for every primitive type (Guid, DateOnly,
        // TimeOnly, decimal-with-German-locale, etc.).
        var query = _client.For<TEntity>(entitySet);
        if (key is IDictionary<string, object> compositeKey)
        {
            string filter = string.Join(
                " and ",
                compositeKey.Select(kv => $"{kv.Key} eq {IntegratoRODataExpressionTranslator.FormatValue(kv.Value)}"));
            query = query.Filter(filter);
        }
        else
        {
            query = query.Key(key);
        }
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

        // Route LINQ expressions through IntegratoRODataExpressionTranslator so [JsonPropertyName]
        // is honoured on property paths. See that file's header for the full rationale.
        if (filter is not null) query = query.Filter(IntegratoRODataExpressionTranslator.ToFilterString(filter));
        if (expand is not null) query = query.Expand(IntegratoRODataExpressionTranslator.ToExpandString(expand));
        if (select is not null) query = query.Select(IntegratoRODataExpressionTranslator.ToSelectString(select));
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
        if (filter is not null) query = query.Filter(IntegratoRODataExpressionTranslator.ToFilterString(filter));

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
        return System.Text.Json.JsonSerializer.Deserialize<TEntity>(json, CaseInsensitiveOptions)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize OData response to {typeof(TEntity).Name}. Response was null or incompatible.");
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
