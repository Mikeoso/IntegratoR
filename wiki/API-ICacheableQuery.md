# ICacheableQuery\<TResponse\>

Interface for queries whose responses can be cached. When a query implements this interface, the `CachingBehaviour` in the MediatR pipeline automatically handles cache lookup and storage.

## Use the Interface

```csharp
public record GetDimensionOrdersQuery(string DataAreaId)
    : ICacheableQuery<Result<IEnumerable<DimensionOrder>>>
{
    public string CacheKey => GenerateCacheKey();
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(30);

    public object[] GetCacheKeyValues() => [DataAreaId];

    public string GenerateCacheKey()
        => $"DimensionOrders:[{string.Join(",", GetCacheKeyValues())}]";

    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object> { { "DataAreaId", DataAreaId } };
}
```

## Interface Definition

```csharp
public interface ICacheableQuery<TResponse> : IQuery<TResponse>
```

`ICacheableQuery<TResponse>` extends `IQuery<TResponse>`, so cacheable queries participate in the same MediatR pipeline as regular queries.

## Members

| Member | Type | Description |
|--------|------|-------------|
| `CacheKey` | `string` | Unique key for cache storage/retrieval. Typically delegates to `GenerateCacheKey()`. |
| `CacheDuration` | `TimeSpan?` | How long to cache the response. `null` bypasses caching for this instance. |
| `GetCacheKeyValues()` | `object[]` | Values that uniquely identify this query instance (used to build the cache key). |
| `GenerateCacheKey()` | `string` | Generates the cache key string from the key values. |

## See Examples

### Implementing a cacheable query

```csharp
public record GetFinancialDimensionsQuery(string DataAreaId)
    : ICacheableQuery<Result<IEnumerable<FinancialDimension>>>
{
    public string CacheKey => GenerateCacheKey();
    public TimeSpan? CacheDuration => TimeSpan.FromHours(1);

    public object[] GetCacheKeyValues() => [DataAreaId];

    public string GenerateCacheKey()
        => $"FinancialDimensions:[{string.Join(",", GetCacheKeyValues())}]";

    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object> { { "DataAreaId", DataAreaId } };
}

// Send the query -- CachingBehaviour handles caching transparently
Result<IEnumerable<FinancialDimension>> result = await mediator.Send(
    new GetFinancialDimensionsQuery("USMF"),
    cancellationToken);
// First call: cache MISS -> executes handler, caches result
// Second call: cache HIT -> returns cached result immediately
```

### Multi-parameter cache key

```csharp
public record GetCustomerByGroupQuery(string CustomerGroup, string DataAreaId)
    : ICacheableQuery<Result<IEnumerable<CustomerEntity>>>
{
    public string CacheKey => GenerateCacheKey();
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(15);

    public object[] GetCacheKeyValues() => [CustomerGroup, DataAreaId];

    public string GenerateCacheKey()
        => $"CustomerByGroup:[{string.Join(",", GetCacheKeyValues())}]";

    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object>
        {
            { "CustomerGroup", CustomerGroup },
            { "DataAreaId", DataAreaId }
        };
}

// Different parameters produce different cache keys:
// "CustomerByGroup:[10,USMF]"
// "CustomerByGroup:[20,USMF]"
```

### Bypassing the cache

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

// Normal call: cached for 5 minutes
await mediator.Send(new GetExchangeRatesQuery("USMF"), cancellationToken);

// Force fresh data: bypasses cache
await mediator.Send(new GetExchangeRatesQuery("USMF", ForceRefresh: true), cancellationToken);
```

### Error handling

Only successful results are cached. If the handler returns a failure, it is not stored:

```csharp
// Handler returns Result.Fail() -> NOT cached
// Next call will execute the handler again, giving it a chance to succeed
Result<IEnumerable<FinancialDimension>> result = await mediator.Send(
    new GetFinancialDimensionsQuery("INVALID"),
    cancellationToken);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    Console.WriteLine($"[{error?.Type}] {error?.Message}");
    // Output: [NotFound] No financial dimensions found for company INVALID
    // This failure is NOT cached -- next call will retry
}
```

## Keep in Mind

- The `CachingBehaviour` is registered third in the pipeline (after Logging and Validation), so invalid requests are rejected before reaching the cache.
- Cache key design is critical for correctness. Include all parameters that affect the query result in `GetCacheKeyValues()`.
- The default `ICacheService` implementation is `InMemoryCacheService`. For scaled-out Azure Functions, replace it with a distributed cache (e.g. Redis).

## See Also

- [[API-Pipeline-Behaviours]] — cache behaviour that checks this interface
- [[API-IQuery]] — base query interface extended by ICacheableQuery
- [[API-AddApplicationServices]] — registers the caching behaviour
