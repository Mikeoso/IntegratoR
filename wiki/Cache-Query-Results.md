# Cache Query Results

> Last verified against v3.0.0

Cache a read-only query by implementing `ICacheableQuery<TResponse>` on it. The `CachingBehaviour` pipeline step checks the cache before the handler runs, and stores the handler's response on a miss. Caching works out of the box — `AddIntegratoR` registers an in-memory cache, so a cacheable query is served from cache without any extra wiring.

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Queries;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;

// A cacheable query supplies CacheKey and CacheDuration, plus the two [Obsolete] members
// the interface still requires.
public record GetDimensionFormatQuery(string DimensionFormat, DimensionHierarchyType HierarchyType)
    : ICacheableQuery<Result<DimensionFormat>>
{
    public string CacheKey => $"{nameof(GetDimensionFormatQuery)}-{DimensionFormat}-{HierarchyType}";

    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(15);

    // CachingBehaviour never calls these; implement them as pass-throughs.
    public string GenerateCacheKey() => CacheKey;

    public object[] GetCacheKeyValues() => [nameof(GetDimensionFormatQuery), DimensionFormat, HierarchyType];

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
> `GenerateCacheKey()` and `GetCacheKeyValues()` are `[Obsolete]` since v2.0.0 and the behaviour reads `CacheKey` directly, but the interface still declares them, so every cacheable query must implement them — the shipped `GetDimensionOrdersQuery` does the same. Do not build new keys through them. v3.0.0 still requires them; removal is planned for a future major version (v4 at the earliest).

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

## Force a fresh read

There is no per-instance bypass: the behaviour looks up every `ICacheableQuery` and stores every successful result. To force the handler to run, evict the entry through the injected `ICacheService` before you send:

```csharp
// cacheService is the ICacheService registered by AddIntegratoR.
await cacheService.RemoveAsync(query.CacheKey);

Result<DimensionFormat> fresh = await mediator.Send(query, cancellationToken);  // cache miss -> handler runs, result re-stored
```

```csharp
// DON'T: a null CacheDuration does not skip the cache. The lookup still runs, and the
// successful result is stored for the cache service's 30-minute default.
public TimeSpan? CacheDuration => ForceRefresh ? null : TimeSpan.FromMinutes(15);
```

To keep separate entries per variant instead, fold the varying input (a version token, a company) into `CacheKey`.

## Choose between in-memory and distributed cache

`AddIntegratoR` registers `InMemoryCacheService` as the `ICacheService`, backed by `IMemoryCache`; a `null` `CacheDuration` stores the entry for a **30-minute default** (`DistributedCacheService` uses the same default). This is the default and needs no configuration.

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
