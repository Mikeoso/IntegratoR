# Cache Query Results

> Last verified against v2.0.1

Cache a read-only query by implementing `ICacheableQuery<TResponse>` on it. The `CachingBehaviour` pipeline step checks the cache before the handler runs, and stores the handler's response on a miss. Caching works out of the box — `AddIntegratoR` registers an in-memory cache, so a cacheable query is served from cache without any extra wiring.

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Queries;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;

// A cacheable query supplies two members: CacheKey and CacheDuration.
public record GetDimensionFormatQuery(string DimensionFormat, DimensionHierarchyType HierarchyType)
    : ICacheableQuery<Result<DimensionFormat>>
{
    public string CacheKey => $"{nameof(GetDimensionFormatQuery)}-{DimensionFormat}-{HierarchyType}";

    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(15);

    public IReadOnlyDictionary<string, object> GetLoggingContext() => new Dictionary<string, object>
    {
        ["DimensionFormat"] = DimensionFormat,
        ["HierarchyType"] = HierarchyType.ToString(),
    };
}
```

Send it through `IMediator` as you would any query. The first call runs the handler and caches the successful `Result<T>`; the second call for the same `CacheKey` returns the cached response and the handler never runs.

```csharp
var query = new GetDimensionFormatQuery("Sachkontodimensionen", DimensionHierarchyType.DataEntityLedgerDimensionFormat);

Result<DimensionFormat> first = await mediator.Send(query, cancellationToken);   // cache miss -> handler runs
Result<DimensionFormat> second = await mediator.Send(query, cancellationToken);  // cache hit  -> handler skipped

if (second.IsSuccess)
{
    // second.Value.Segments -> ["MainAccount", "A_Kostenstelle", "C_Profitcenter"]
}
```

`ICacheableQuery<TResponse>` already extends `IQuery<TResponse>`, so declare only `ICacheableQuery` — never list `IQuery<...>` as well. `GetLoggingContext()` comes from the shared `IContext` base and is the same member every command and query carries.

> [!NOTE]
> `ICacheableQuery` also declares `GenerateCacheKey()` and `GetCacheKeyValues()`, but both are `[Obsolete]` (since v1.4.0) and the behaviour reads `CacheKey` directly. Do not build new keys through them; `GetDimensionOrdersQuery` still implements them only to satisfy the interface until the next MAJOR removes them.

## Handle the failure path

Only successful results are cached. A failed `Result<T>` — a `NotFound`, a validation rejection, an authentication failure — flows back to the caller and is never stored, so the next call retries the handler rather than replaying the error.

```csharp
Result<DimensionFormat> result = await mediator.Send(query, cancellationToken);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    // Missing singleton parameter row in this company:
    //   error?.Code -> "DimensionParameters.NotFound"
    //   error?.Type -> ErrorType.NotFound
    return result;
}
```

Failure-non-caching is deliberate: a transient D365 outage or a `NotFound` on the first call must not poison the cache for the entries that follow.

## Bypass the cache for one instance

Return `null` from `CacheDuration` to skip caching for a specific query instance while the query type stays cacheable. `CacheDuration` is read after the lookup, so a `null` still allows a cache hit — it only prevents storing the response.

```csharp
public record GetDimensionFormatQuery(string DimensionFormat, DimensionHierarchyType HierarchyType, bool ForceRefresh)
    : ICacheableQuery<Result<DimensionFormat>>
{
    public string CacheKey => $"{nameof(GetDimensionFormatQuery)}-{DimensionFormat}-{HierarchyType}";

    public TimeSpan? CacheDuration => ForceRefresh ? null : TimeSpan.FromMinutes(15);

    public IReadOnlyDictionary<string, object> GetLoggingContext() => new Dictionary<string, object>
    {
        ["DimensionFormat"] = DimensionFormat,
    };
}
```

To force a fresh fetch that also misses the lookup, vary `CacheKey` as well (fold a version token into it) so the behaviour cannot return an already-stored response.

## Choose between in-memory and distributed cache

`AddIntegratoR` registers `InMemoryCacheService` as the `ICacheService`, backed by `IMemoryCache` with a **30-minute default** duration when `CacheDuration` is `null`. This is the default and needs no configuration.

**Keep the in-memory default for a single-instance host.** Switch to `DistributedCacheService` only when cache consistency across scaled-out worker instances matters — it shares one Redis-backed store, so every instance sees the same entries.

```csharp
// In-memory (default): already wired by AddIntegratoR. Process-local; resets on host restart.
builder.Services.AddIntegratoR(configuration);

// Distributed (opt-in): register an IDistributedCache, then replace ICacheService.
// Do this AFTER AddIntegratoR so the later registration wins.
builder.Services.AddIntegratoR(configuration);
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "IntegratoR:";
});
builder.Services.AddSingleton<ICacheService, DistributedCacheService>();
```

`DistributedCacheService` serialises each response with `System.Text.Json` and calls `.AddResultConverters()` on its own serialiser options, so a cached `Result<T>` round-trips through Redis with its error shape intact. You register nothing extra for that.

- **Choose in-memory when** the host is a single instance, or the cached data is cheap to recompute after a restart (dimension formats, reference lookups).
- **Choose distributed when** the host scales out and instances must agree on cached values, or a restart must not cold-start every cache.

> [!NOTE]
> `DistributedCacheService` and the Durable Functions data converter both feed the same `Result<T>` System.Text.Json converters; `AddIntegratoR` keeps that wiring in lockstep. Newtonsoft.Json paths in your host (HTTP request bodies) are wired separately — see [Set Up Azure Functions Host](Set-Up-Azure-Functions-Host).

## Write a stable cache key

The behaviour trusts `CacheKey` completely: two instances that share a key share a cached value. Build the key so it varies with every input that varies the response.

```csharp
// DON'T: an unstable key. Object.GetHashCode() differs across restarts, so the cache never hits.
public string CacheKey => $"dim-{GetHashCode()}";

// DO: a stable, type-prefixed key from the query's own inputs.
public string CacheKey => $"{nameof(GetDimensionFormatQuery)}-{DimensionFormat}-{HierarchyType}";
```

- Include every input that changes the result; omit anything that does not.
- Prefix with the query type name so a shared cache stays diagnosable.
- Keep identifiers, not names or descriptions — cache keys surface in `Debug` logs, so avoid personal data.

## Verify the cache in tests

`IntegratoR.TestKit` ships `FakeCacheService`, an in-memory `ICacheService` with `Contains`, `Count`, and `Clear` helpers. Register it as the `ICacheService`, run the handler through the pipeline, then assert the entry landed under the expected key.

```csharp
var cache = new FakeCacheService();
services.AddSingleton<ICacheService>(cache);
// ... build the provider, send the query via IMediator ...

cache.Contains("GetDimensionFormatQuery-Sachkontodimensionen-DataEntityLedgerDimensionFormat")
    .Should().BeTrue();
cache.Count.Should().Be(1);
```

`FakeCacheService` stores entries forever and ignores `CacheDuration`. To assert expiry semantics, run `DistributedCacheService` against a `MemoryDistributedCache` instead.

## See Also

- [Run Queries](Run-Queries) — the query types that opt into caching
- [Extend the Pipeline](Extend-the-Pipeline) — where `CachingBehaviour` sits in the order
- [Handle Errors](Handle-Errors) — only successful `Result<T>` responses are cached
- [Test with TestKit](Test-with-TestKit) — `FakeCacheService` and its assertions
