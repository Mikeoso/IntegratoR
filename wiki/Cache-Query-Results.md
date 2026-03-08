# Cache Query Results

Implement `ICacheableQuery<TResponse>` on a query to have the `CachingBehaviour` automatically cache successful responses. This reduces load on the D365 F&O OData endpoint for frequently-read, slowly-changing data.

> **Prerequisites:** [[Install-the-Framework]]

## Implement ICacheableQuery on a Query

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Queries;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

public record GetJournalsByCompanyQuery(string DataAreaId)
    : ICacheableQuery<Result<IEnumerable<LedgerJournalHeader>>>
{
    public string CacheKey => GenerateCacheKey();

    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);

    public object[] GetCacheKeyValues()
    {
        return new object[] { nameof(GetJournalsByCompanyQuery), DataAreaId };
    }

    public string GenerateCacheKey()
    {
        return $"{nameof(GetJournalsByCompanyQuery)}-{DataAreaId}";
    }

    public IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return new Dictionary<string, object>
        {
            { "DataAreaId", DataAreaId }
        };
    }
}
```

| Property / Method | Purpose |
|-------------------|---------|
| `CacheKey` | Unique key for cache storage and retrieval |
| `CacheDuration` | How long the response stays cached. `null` bypasses caching. |
| `GetCacheKeyValues()` | Values that make this query instance unique |
| `GenerateCacheKey()` | Builds the cache key string from the key values |

## How the CachingBehaviour Works

The `CachingBehaviour` sits in the MediatR pipeline and intercepts any request implementing `ICacheableQuery<TResponse>`:

1. **Cache hit** -- returns the cached response immediately without invoking the handler.
2. **Cache miss** -- invokes the handler, then caches the response if `IsSuccess` is true.
3. **Failed results are never cached** -- this prevents caching transient errors or `NotFound` responses.

```
Request -> LoggingBehaviour -> ValidationBehaviour -> CachingBehaviour -> Handler
                                                          |
                                                  ICacheableQuery?
                                                  /              \
                                                No                Yes
                                                |                  |
                                          Pass through      Check cache
                                                            /          \
                                                         Hit          Miss
                                                          |             |
                                                    Return cached   Run handler
                                                                        |
                                                                   IsSuccess?
                                                                   /        \
                                                                 Yes        No
                                                                  |          |
                                                            Cache result   Return as-is
                                                            and return
```

## Real-World Example: GetDimensionOrdersQuery

The `GetDimensionOrdersQuery` in the OData.FO layer caches dimension format data for 15 minutes:

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Queries;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;

public record GetDimensionOrdersQuery(
    string dimensionFormat,
    DimensionHierarchyType hierarchyType)
    : ICacheableQuery<Result<DimensionFormat>>
{
    public string CacheKey =>
        $"{nameof(GetDimensionOrdersQuery)}-{dimensionFormat}-{hierarchyType}";

    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(15);

    public string GenerateCacheKey() => CacheKey;

    public object[] GetCacheKeyValues()
    {
        return new object[]
        {
            nameof(GetDimensionOrdersQuery),
            dimensionFormat,
            hierarchyType
        };
    }

    public IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return new Dictionary<string, object>
        {
            { "DimensionFormat", dimensionFormat },
            { "HierarchyType", hierarchyType.ToString() }
        };
    }
}
```

This is ideal for dimension configuration data that rarely changes but is queried on every journal line creation.

## Bypass the Cache

Set `CacheDuration` to `null` to skip caching for a specific query instance:

```csharp
public TimeSpan? CacheDuration => forceRefresh ? null : TimeSpan.FromMinutes(10);
```

When `CacheDuration` is `null`, the `CachingBehaviour` still checks for an existing cached entry but will not store a new one.

## When Things Go Wrong

**Stale cache** -- if the underlying data changes in D365 before the cache expires, you will receive outdated results. Choose `CacheDuration` values appropriate for your data's change frequency. Configuration data (dimensions, tax groups) can safely be cached for 15-60 minutes. Transactional data should not be cached or should use very short durations.

**Cache service unavailable** -- if the `ICacheService` implementation (e.g. Redis) is unavailable, the `CachingBehaviour` will fall through to the handler. The handler executes normally but results are not cached.

## Avoid Common Pitfalls

- **Don't cache transactional data** that changes more frequently than the cache duration -- balances, pending invoices, and in-flight journal lines are poor candidates.
- **Volatile reference data** such as rapidly-changing exchange rates should use very short durations or skip caching entirely.
- **Cache invalidation is not automatic** -- the framework does not clear cached entries when the underlying D365 data changes, so stale data can silently cause incorrect business logic.

## See Also

- [[Query-Entities-by-Filter]] — filter queries that benefit from caching
- [[Query-Entities-by-Key]] — key lookups that benefit from caching
- [[Configure-Retry-and-Circuit-Breaker]] — resilience policies that complement caching
