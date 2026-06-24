# Cache Query Results

Caching is implemented as a MediatR pipeline behaviour. A query opts into caching by implementing `ICacheableQuery<TResponse>`. The `CachingBehaviour` intercepts the request, checks the configured `ICacheService`, and either returns the cached `Result<T>` or runs the handler and stores its successful response.

> **Prerequisites:** a configured OData client and a distributed cache (`IDistributedCache`) registered through `AddIntegratoR` — see [Set Up Azure Functions Host](Set-Up-Azure-Functions-Host) for both. Without a registered `IDistributedCache`, the `CachingBehaviour` has no backing store and every request falls through to the handler.

## Make a Query Cacheable

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Queries;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;

public record GetDimensionOrdersQuery(string dimensionFormat, DimensionHierarchyType hierarchyType)
    : ICacheableQuery<Result<DimensionFormat>>
{
    public string CacheKey => $"{nameof(GetDimensionOrdersQuery)}-{dimensionFormat}-{hierarchyType}";

    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(15);

    public string GenerateCacheKey() => CacheKey;

    public object[] GetCacheKeyValues() => [nameof(GetDimensionOrdersQuery), dimensionFormat, hierarchyType];

    public IReadOnlyDictionary<string, object> GetLoggingContext() => new Dictionary<string, object>
    {
        { "DimensionFormat", dimensionFormat },
        { "HierarchyType", hierarchyType.ToString() }
    };
}
```

`ICacheableQuery<T>` already extends `IQuery<T>`, so you implement only `ICacheableQuery` — do not also list `IQuery<...>` in the declaration. Because `IQuery` derives from `IContext`, a cacheable query must implement five members:

| Member | Purpose |
|---|---|
| `string CacheKey { get; }` | Unique key under which the response is stored. Typically delegates to `GenerateCacheKey()`. |
| `TimeSpan? CacheDuration { get; }` | How long the response is cached. `null` bypasses caching for this specific instance. |
| `string GenerateCacheKey()` | Builds the cache key from `GetCacheKeyValues()`. |
| `object[] GetCacheKeyValues()` | Values that uniquely identify this query instance. Used as input for `GenerateCacheKey()`. |
| `IReadOnlyDictionary<string, object> GetLoggingContext()` | Structured context fields surfaced by `LoggingBehaviour` (required by `IContext`, the base of every `IQuery`). |

A common implementation pattern: concatenate the query type name with a stable serialised form of the key values. The framework does not enforce a specific format — only that two queries with different parameters yield two different `CacheKey` strings.

## Pipeline Flow

The `CachingBehaviour<TRequest, TResponse>` runs after logging and validation in the pipeline — see [Extend the Pipeline](Extend-the-Pipeline) for the canonical ordering. On each request:

1. If the request is **not** `ICacheableQuery<TResponse>`, pass straight through to the next behaviour.
2. Resolve `CacheKey`. Call `_cacheService.GetAsync<TResponse>(cacheKey)`.
3. If the cache returns a non-null value, log a cache hit at `Debug` and return immediately (the handler does **not** run).
4. If the cache misses, run the handler, get the `Result<T>`.
5. Cache the result **only if successful** (`response.IsSuccess == true`). Failed results are never cached so the next call retries.

Failure-non-caching is intentional. A `NotFound` or `AuthenticationFailed` response on the first call must not poison the cache.

## Bypass Cache for a Specific Instance

Setting `CacheDuration` to `null` on a particular query instance bypasses the cache even when the query type opts in:

```csharp
public record GetFreshDimensionFormatQuery(string dimensionFormat, DimensionHierarchyType hierarchyType, bool ForceRefresh)
    : ICacheableQuery<Result<DimensionFormat>>
{
    public string CacheKey =>
        $"GetDimensionOrdersQuery-{dimensionFormat}-{hierarchyType}";

    public TimeSpan? CacheDuration => ForceRefresh ? null : TimeSpan.FromMinutes(15);

    public object[] GetCacheKeyValues() => [dimensionFormat, hierarchyType, ForceRefresh];
    public string GenerateCacheKey() => CacheKey;
}
```

`CacheDuration` is read **after** the cache lookup. A `null` value still permits a cache hit on this call — it only prevents storing the response. Consumers wanting to force a fresh fetch should additionally vary `CacheKey` (include a versioning input) so the lookup misses.

## In-Memory vs Distributed Cache

The framework registers `ICacheService` against a concrete implementation that wraps `IDistributedCache`. The choice of in-memory vs distributed is determined by which `IDistributedCache` the consumer registers in DI:

```csharp
// In-memory (process-local; resets on host restart)
services.AddDistributedMemoryCache();

// Redis (shared across worker instances)
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "your-redis-connection-string";
    options.InstanceName = "IntegratoR:";
});
```

The framework calls `IDistributedCache` indirectly via `DistributedCacheService` (in `IntegratoR.Application`). The service serialises responses with `System.Text.Json` and registers the `Result<T>` STJ converter so `Result` instances round-trip correctly.

> The STJ `Result<T>` converters are wired automatically by `AddIntegratoR`. Consumers using Newtonsoft.Json elsewhere in the host (e.g. for HTTP request bodies) need to wire the Newtonsoft converters separately — see [Set Up Azure Functions Host](Set-Up-Azure-Functions-Host).

## Cache Key Best Practices

- **Include every input that varies the response.** Two queries that produce different results must have different cache keys, otherwise one returns the other's stale value.
- **Use a stable serialisation.** Object hashes (`Object.GetHashCode()`) are not stable across runtime restarts. Prefer string interpolation or `JsonSerializer.Serialize(GetCacheKeyValues())`.
- **Prefix with the query type name.** `GetDimensionOrdersQuery-...` is preferable to a bare key — multi-query caches stay diagnosable.
- **Avoid PII in keys.** Cache keys leak into logs at `Debug` level. Use stable identifiers (entity IDs, codes) rather than names or descriptions.

## Cache Duration Guidance

| Data type | Suggested duration |
|---|---|
| Dimension formats, parameters, reference data | 15 minutes to 1 hour |
| User-specific configuration | 5 minutes |
| Per-request lookups (within one operation) | Use a scoped service, not the cache |
| Anything that changes per business transaction | Do not cache |

The 15-minute default used by `GetDimensionOrdersQuery` is calibrated for D365 environments where dimension setup changes infrequently. Adjust per query based on the underlying data's volatility.

## Observability

The behaviour emits at `Debug` level:

- `"Cache HIT for key {CacheKey}. Returning cached response."` — handler skipped
- `"Cache MISS for key {CacheKey}. Executing handler."` — handler ran
- `"Handler executed successfully. Caching response with key {CacheKey} for {CacheDuration}"` — response cached

Hit/miss ratios are useful for tuning `CacheDuration`. A constant miss rate suggests the duration is too short for the access pattern; a 100 % hit rate suggests caching is unnecessary (the handler is never being exercised).

## Testing Cache Behaviour

The `IntegratoR.TestKit` ships `FakeCacheService` for in-memory verification:

```csharp
var cache = new FakeCacheService();
// ... wire it as ICacheService, run the handler ...
cache.Contains("GetDimensionOrdersQuery-Sachkontodimensionen-DataEntityLedgerDimensionFormat").Should().BeTrue();
cache.Count.Should().Be(1);
```

See [Test with TestKit](Test-with-TestKit).

## See Also

- [Run Queries](Run-Queries) — the query types that can opt into caching
- [Set Up Azure Functions Host](Set-Up-Azure-Functions-Host) — registering an `IDistributedCache` implementation
- [Test with TestKit](Test-with-TestKit) — `FakeCacheService` and cache-related assertions
- [Handle Errors](Handle-Errors) — only successful `Result` instances are cached
