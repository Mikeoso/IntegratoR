# Caching

```csharp
// Implement ICacheableQuery<TResponse> (extends IQuery<TResponse>) to cache query results automatically
public record GetJournalsByCompanyQuery(string DataAreaId)
    : ICacheableQuery<Result<IEnumerable<LedgerJournalHeader>>>
{
    public string CacheKey => GenerateCacheKey();
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);

    public object[] GetCacheKeyValues() => [nameof(GetJournalsByCompanyQuery), DataAreaId];
    public string GenerateCacheKey() => $"{nameof(GetJournalsByCompanyQuery)}-{DataAreaId}";

    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object> { { "DataAreaId", DataAreaId } };
}

Result<IEnumerable<LedgerJournalHeader>> result = await mediator.Send(
    new GetJournalsByCompanyQuery("USMF"), cancellationToken);
// First call: cache MISS -> handler runs, result cached
// Second call: cache HIT -> cached result returned immediately
```

## ICacheableQuery Members

| Member | Type | Purpose |
|--------|------|---------|
| `CacheKey` | `string` | Unique key for cache storage/retrieval. Delegates to `GenerateCacheKey()`. |
| `CacheDuration` | `TimeSpan?` | How long to cache the response. `null` bypasses caching. |
| `GetCacheKeyValues()` | `object[]` | Values that uniquely identify this query instance. |
| `GenerateCacheKey()` | `string` | Builds the cache key string from the key values. |

`CacheKey` typically delegates to `GenerateCacheKey()`. Keep `GetCacheKeyValues()` as the single source of truth for which parameters affect caching — `GenerateCacheKey()` should only format those values into a string.

## CachingBehaviour Pipeline

The [[Extending-the-Pipeline]] run in order: Logging -> Validation -> **Caching** -> Handler. Invalid requests are rejected before reaching the cache.

- **Cache hit** -- returns the cached response without invoking the handler.
- **Cache miss** -- invokes the handler, caches the response only if `IsSuccess` is true.
- **Failed results are never cached** -- transient errors and `NotFound` responses always re-execute.

## Multi-Parameter Cache Keys

Include all parameters that affect the result in `GetCacheKeyValues()`:

```csharp
public record GetCustomerByGroupQuery(string CustomerGroup, string DataAreaId)
    : ICacheableQuery<Result<IEnumerable<CustomerEntity>>>
{
    public string CacheKey => GenerateCacheKey();
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(15);

    public object[] GetCacheKeyValues() => [CustomerGroup, DataAreaId];
    public string GenerateCacheKey()
        => $"CustomerByGroup:[{string.Join(",", GetCacheKeyValues())}]";
    // "CustomerByGroup:[10,USMF]" and "CustomerByGroup:[20,USMF]" are separate entries

    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object>
        {
            { "CustomerGroup", CustomerGroup },
            { "DataAreaId", DataAreaId }
        };
}
```

## Bypassing the Cache

Set `CacheDuration` to `null` to skip caching for a specific query instance:

```csharp
public record GetExchangeRatesQuery(string DataAreaId, bool ForceRefresh = false)
    : ICacheableQuery<Result<IEnumerable<ExchangeRate>>>
{
    public string CacheKey => GenerateCacheKey();
    public TimeSpan? CacheDuration => ForceRefresh ? null : TimeSpan.FromMinutes(5);

    public object[] GetCacheKeyValues() => [DataAreaId];
    public string GenerateCacheKey()
        => $"ExchangeRates:[{string.Join(",", GetCacheKeyValues())}]";

    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object> { { "DataAreaId", DataAreaId } };
}

await mediator.Send(new GetExchangeRatesQuery("USMF"), cancellationToken);                        // cached 5 min
await mediator.Send(new GetExchangeRatesQuery("USMF", ForceRefresh: true), cancellationToken);    // bypasses cache
```

## Cache Service

The default `ICacheService` implementation is `InMemoryCacheService`. For scaled-out Azure Functions, replace it with a distributed cache (e.g. Redis). If the cache service is unavailable, the `CachingBehaviour` falls through to the handler and results are not cached.

## See Also

- [[Queries]] — query patterns that caching wraps
- [[Extending-the-Pipeline]] — pipeline behaviour order (caching runs after validation)
- [[Testing]] — `FakeCacheService` for testing cached queries
- [[Configuration]] — settings reference
