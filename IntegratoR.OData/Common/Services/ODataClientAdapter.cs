using System.Linq.Expressions;
using System.Net;
using System.Text;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.OData.Common.Filters;
using IntegratoR.OData.Domain.Models;
using IntegratoR.OData.Interfaces.Services;
using PanoramicData.OData.Client;
using PanoramicData.OData.Client.Exceptions;

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
    private readonly IHttpClientFactory? _httpClientFactory;

    /// <summary>
    /// Creates an adapter over PanoramicData's <see cref="ODataClient"/> without a named-client
    /// factory. Composite-key write operations (Update/Delete/BatchUpdate/BatchDelete on an
    /// <see cref="IDictionary{TKey, TValue}"/> key) are NOT supported through this constructor and
    /// throw <see cref="InvalidOperationException"/>; production DI always uses the two-argument
    /// constructor. Retained for guard tests that never issue HTTP traffic.
    /// </summary>
    public ODataClientAdapter(ODataClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Creates an adapter over PanoramicData's <see cref="ODataClient"/> with the named-client
    /// factory required by the composite-key write bypass. The factory must resolve the
    /// <c>"ODataClient"</c> named client so raw composite-key requests carry the same
    /// authentication, Polly resilience, and base address as PanoramicData's own traffic.
    /// </summary>
    public ODataClientAdapter(ODataClient client, IHttpClientFactory httpClientFactory)
    {
        _client = client;
        _httpClientFactory = httpClientFactory;
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
            if (compositeKey.Count == 0)
            {
                throw new ArgumentException(
                    "Composite key dictionary must contain at least one key field. " +
                    "An empty dictionary would emit a blank $filter and return an arbitrary row.",
                    nameof(key));
            }

            // Each dictionary key is interpolated verbatim into the OData $filter. Reject any
            // key that does not look like a simple OData property identifier so a caller that
            // bypasses ODataService and hands the adapter a tainted dictionary cannot inject
            // additional filter clauses (e.g. "JournalNum eq '1' or 1 eq 1"). Framework-internal
            // callers go through ODataService.BuildCompositeKeyObject which derives keys from
            // entity reflection via PropertyNameResolver, so they always pass this check.
            foreach (KeyValuePair<string, object> kv in compositeKey)
            {
                if (!IsValidODataFieldName(kv.Key))
                {
                    throw new ArgumentException(
                        $"Composite key field name '{kv.Key}' is not a valid OData property identifier. " +
                        "Keys must match the pattern ^[A-Za-z_][A-Za-z0-9_.]*$ and come from entity " +
                        "reflection (attribute-derived wire names), not user input.",
                        nameof(key));
                }
            }

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

    /// <summary>
    /// Validates that a composite-key dictionary key matches a simple OData property identifier
    /// shape: starts with a letter or underscore, followed by letters, digits, underscores, or
    /// dots (to permit qualified names like <c>Namespace.Field</c>). Keys that fail this check
    /// are rejected rather than interpolated into <c>$filter</c>.
    /// </summary>
    private static bool IsValidODataFieldName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        char first = name[0];
        if (!(char.IsLetter(first) || first == '_'))
        {
            return false;
        }

        for (int i = 1; i < name.Length; i++)
        {
            char c = name[i];
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '.'))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> FindEntriesAsync<TEntity>(
        string entitySet,
        Expression<Func<TEntity, bool>>? filter = null,
        Expression<Func<TEntity, object>>? expand = null,
        Expression<Func<TEntity, object>>? select = null,
        IReadOnlyList<(Expression<Func<TEntity, object>> KeySelector, bool Descending)>? orderBy = null,
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
        if (orderBy is not null && orderBy.Count > 0) query = query.OrderBy(IntegratoRODataExpressionTranslator.ToOrderByString(orderBy));
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
        // Composite (dictionary) keys cannot be bound by PanoramicData's Key(object) path (it
        // calls .ToString() on the dict, producing a malformed key) — route them through the
        // raw-HttpClient bypass. See SendCompositeKeyRequestAsync and FindByKeyAsync's header.
        if (key is IDictionary<string, object> compositeKey)
        {
            return await SendCompositeKeyRequestAsync<TEntity>(
                HttpMethod.Patch, entitySet, compositeKey, payload, cancellationToken).ConfigureAwait(false);
        }

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
        // Composite (dictionary) keys bypass PanoramicData's broken Key(object) path; the
        // throwaway TEntity type parameter is unused for DELETE (no response body deserialised).
        if (key is IDictionary<string, object> compositeKey)
        {
            await SendCompositeKeyRequestAsync<object>(
                HttpMethod.Delete, entitySet, compositeKey, payload: null, cancellationToken).ConfigureAwait(false);
            return;
        }

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
        var itemList = items as IList<(object Key, IDictionary<string, object> Payload)> ?? items.ToList();

        // A batch is homogeneous for a given TEntity: ODataService.BuildBatchKeys yields all-scalar
        // keys for single-key entities or all-dictionary keys for composite entities, never mixed.
        // D365 F&O entities are all composite-keyed and cannot use PanoramicData's atomic changeset
        // (it cannot bind a dictionary key), so when any key is a dictionary, send EVERY item as an
        // individual HTTP request preserving index order; otherwise keep the atomic changeset path.
        if (itemList.Any(item => item.Key is IDictionary<string, object>))
        {
            return await SendCompositeKeyBatchAsync(
                itemList.Count,
                async (index, ct) =>
                {
                    // isComposite invariant (the homogeneity above) guarantees this cast is safe.
                    var (key, payload) = itemList[index];
                    return await SendCompositeKeyRequestRawAsync(
                        HttpMethod.Patch, entitySet, (IDictionary<string, object>)key, payload, ct).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }

        var batch = _client.CreateBatch();
        batch.Changeset(changeset =>
        {
            foreach (var (key, payload) in itemList)
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
        var keyList = keys as IList<object> ?? keys.ToList();

        // Homogeneous-batch invariant (see BatchUpdateAsync): a composite-keyed entity yields all
        // dictionary keys, so when any key is a dictionary, send EVERY item as an individual HTTP
        // request preserving index order; otherwise keep the atomic changeset path.
        if (keyList.Any(key => key is IDictionary<string, object>))
        {
            return await SendCompositeKeyBatchAsync(
                keyList.Count,
                // isComposite invariant guarantees keyList[index] is an IDictionary here.
                async (index, ct) => await SendCompositeKeyRequestRawAsync(
                    HttpMethod.Delete, entitySet, (IDictionary<string, object>)keyList[index], payload: null, ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }

        var batch = _client.CreateBatch();
        batch.Changeset(changeset =>
        {
            foreach (var key in keyList)
            {
                changeset.Delete(entitySet, key);
            }
        });
        ODataBatchResponse response = await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return MapBatchResponse(response);
    }

    /// <summary>
    /// Issues a single composite-key write (PATCH/DELETE) through the named <c>"ODataClient"</c>
    /// HttpClient (which carries auth + Polly + BaseAddress), bypassing PanoramicData's broken
    /// <c>Key(object)</c> dictionary path. On success a PATCH body is deserialised to
    /// <typeparamref name="TEntity"/>; on a non-success status the matching PanoramicData
    /// exception is thrown so the existing <c>ODataExceptionHandler</c> maps it (404 →
    /// <see cref="ODataNotFoundException"/>; otherwise <see cref="ODataClientException"/> carrying
    /// status, body, and URL).
    /// </summary>
    private async Task<TEntity> SendCompositeKeyRequestAsync<TEntity>(
        HttpMethod method,
        string entitySet,
        IDictionary<string, object> key,
        object? payload,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        (HttpStatusCode statusCode, string? body, string requestUrl) =
            await SendCompositeKeyRequestRawAsync(method, entitySet, key, payload, cancellationToken).ConfigureAwait(false);

        if ((int)statusCode is < 200 or > 299)
        {
            throw CreateMappableException((int)statusCode, body, requestUrl);
        }

        // DELETE has no usable response body; callers discard the returned value.
        if (string.IsNullOrWhiteSpace(body))
        {
            return null!;
        }

        return System.Text.Json.JsonSerializer.Deserialize<TEntity>(body, CaseInsensitiveOptions)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize OData response to {typeof(TEntity).Name}. Response body was null or incompatible.");
    }

    /// <summary>
    /// Issues a single composite-key write and returns the raw outcome (status, body, URL) WITHOUT
    /// throwing — used by the batch path so per-item failures are collected rather than aborting
    /// the whole batch. Validates each key field name and the dictionary is non-empty, mirroring
    /// <see cref="FindByKeyAsync{TEntity}"/>.
    /// </summary>
    private async Task<(HttpStatusCode StatusCode, string? Body, string RequestUrl)> SendCompositeKeyRequestRawAsync(
        HttpMethod method,
        string entitySet,
        IDictionary<string, object> key,
        object? payload,
        CancellationToken cancellationToken)
    {
        if (_httpClientFactory is null)
        {
            throw new InvalidOperationException(
                "Composite-key write operations require the IHttpClientFactory-based constructor of " +
                "ODataClientAdapter. Production DI uses the two-argument constructor; the single-argument " +
                "constructor is for guard tests that never issue HTTP traffic.");
        }

        // requestUrl is a NON-ROOTED relative URI ("EntitySet(...)", no leading '/'). It is resolved
        // against the named client's HttpClient.BaseAddress, which ApplicationDependencyInjection
        // always sets via NormaliseBaseUrl (url.TrimEnd('/') + "/"), i.e. it ALWAYS ends with '/'.
        // That trailing slash is what makes a non-rooted relative compose correctly and PRESERVE the
        // base path segment (e.g. "https://host/fo/" + "LedgerJournalHeaders(...)" =>
        // "https://host/fo/LedgerJournalHeaders(...)"). Without it, Uri composition would drop "/fo".
        string requestUrl = BuildCompositeKeyUrl(entitySet, key);

        using var request = new HttpRequestMessage(method, requestUrl);
        if (payload is not null && method != HttpMethod.Delete)
        {
            string json = System.Text.Json.JsonSerializer.Serialize(payload, CaseInsensitiveOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        HttpClient client = _httpClientFactory.CreateClient("ODataClient");
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        string? body = response.Content is null
            ? null
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return (response.StatusCode, body, requestUrl);
    }

    /// <summary>
    /// Issues every item of a composite-keyed batch as an individual HTTP request (composite-keyed
    /// entities cannot use PanoramicData's atomic changeset), assembling a per-item
    /// <see cref="BatchOperationResult"/> in original index order so the existing
    /// <c>MapBatchResponse</c>/<c>BuildBatchErrors</c> contract is preserved. Per-item failures are
    /// collected, never thrown — so a partial failure surfaces as failed indices, not an exception.
    /// </summary>
    private static async Task<IReadOnlyList<BatchOperationResult>> SendCompositeKeyBatchAsync(
        int count,
        Func<int, CancellationToken, Task<(HttpStatusCode StatusCode, string? Body, string RequestUrl)>> sendComposite,
        CancellationToken cancellationToken)
    {
        var results = new List<BatchOperationResult>(count);

        for (int i = 0; i < count; i++)
        {
            (HttpStatusCode statusCode, string? body, _) =
                await sendComposite(i, cancellationToken).ConfigureAwait(false);

            int code = (int)statusCode;
            bool isSuccess = code is >= 200 and <= 299;
            results.Add(new BatchOperationResult
            {
                Index = i,
                StatusCode = code,
                IsSuccess = isSuccess,
                ErrorMessage = isSuccess ? null : $"HTTP {code}",
                ResponseBody = body
            });
        }

        return results;
    }

    /// <summary>
    /// Builds the keyed URL segment <c>EntitySet(field=literal,…)</c> for a composite key, reusing
    /// the same OData v4 literal formatter (<see cref="IntegratoRODataExpressionTranslator.FormatValue"/>)
    /// as the filter/read path. Validates each field name and rejects empty dictionaries, mirroring
    /// <see cref="FindByKeyAsync{TEntity}"/>.
    /// </summary>
    private static string BuildCompositeKeyUrl(string entitySet, IDictionary<string, object> key)
    {
        if (key.Count == 0)
        {
            throw new ArgumentException(
                "Composite key dictionary must contain at least one key field. " +
                "An empty dictionary would emit a keyless URL segment.",
                nameof(key));
        }

        foreach (KeyValuePair<string, object> kv in key)
        {
            if (!IsValidODataFieldName(kv.Key))
            {
                throw new ArgumentException(
                    $"Composite key field name '{kv.Key}' is not a valid OData property identifier. " +
                    "Keys must match the pattern ^[A-Za-z_][A-Za-z0-9_.]*$ and come from entity " +
                    "reflection (attribute-derived wire names), not user input.",
                    nameof(key));
            }
        }

        string segments = string.Join(
            ",",
            key.Select(kv => $"{kv.Key}={IntegratoRODataExpressionTranslator.FormatValue(kv.Value)}"));

        return $"{entitySet}({segments})";
    }

    /// <summary>
    /// Maps a non-success composite-key write response to the PanoramicData exception the existing
    /// <c>ODataExceptionHandler</c> expects: 404 → <see cref="ODataNotFoundException"/>; any other
    /// non-2xx → <see cref="ODataClientException"/> carrying status, body, and request URL.
    /// </summary>
    private static ODataClientException CreateMappableException(int statusCode, string? responseBody, string requestUrl)
    {
        if (statusCode == 404)
        {
            return new ODataNotFoundException(
                $"Entity at '{requestUrl}' was not found (HTTP 404).", requestUrl);
        }

        return new ODataClientException(
            $"OData request to '{requestUrl}' failed with HTTP {statusCode}.",
            statusCode,
            responseBody,
            requestUrl);
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
