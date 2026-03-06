# PanoramicData.OData.Client — Future Features

PanoramicData.OData.Client (v10.0.55) provides several features beyond what was available in Simple.OData.Client. This document catalogues capabilities available for future adoption.

## Typed Exception Hierarchy

PanoramicData provides a rich exception hierarchy under `PanoramicData.OData.Client.Exceptions`:

| Exception | HTTP Status | Use Case |
|-----------|-------------|----------|
| `ODataNotFoundException` | 404 | Entity not found |
| `ODataUnauthorizedException` | 401 | Authentication required |
| `ODataForbiddenException` | 403 | Access denied |
| `ODataConcurrencyException` | 412 | ETag mismatch (optimistic concurrency) |
| `ODataAsyncOperationException` | — | Long-running async operation failed |
| `ODataClientException` | Any | Base class with `StatusCode`, `ResponseBody`, `RequestUrl` |

**Current usage:** `ODataExceptionHandler` already maps these to `IntegrationError`. The `ODataConcurrencyException` exposes `RequestETag` and `CurrentETag` for richer conflict resolution in the future.

## Built-in Retry with RetryCount/RetryDelay

`ODataClientOptions` includes:
- `RetryCount` (int) — number of automatic retries
- `RetryDelay` (TimeSpan) — delay between retries

**Potential:** Could simplify the dual-Polly-policy setup in `ApplicationDependencyInjection.cs`. Currently, we have both HTTP-level and OData-operation-level Polly retry policies. PanoramicData's built-in retry could replace the OData-operation-level policy, reducing configuration.

**Consideration:** Polly provides jitter and circuit breaking which PanoramicData's built-in retry does not. Keep Polly for HTTP-level resilience; evaluate PanoramicData retry for simpler operation-level retries.

## GetAllAsync — Automatic Pagination

```csharp
var allProducts = await client.GetAllAsync(query, cancellationToken);
```

Automatically follows `@odata.nextLink` to retrieve all pages. Returns `IReadOnlyList<T>`.

**Potential:** Could replace the current `FindAll()` implementation which relies on a single request. D365 F&O paginates at ~10,000 records — `GetAllAsync` would handle this transparently.

**Consideration:** Memory pressure for very large datasets. Consider streaming alternatives for bulk operations.

## Changeset-Level Result Inspection

Batch operations return `ODataBatchResponse` with:
- `AllSucceeded` — quick boolean check
- `HasErrors` / `FailedResults` — identify specific failures
- `GetResult<T>(index)` — retrieve individual operation results
- `ODataBatchOperationResult` — per-operation `StatusCode`, `IsSuccess`, `ErrorMessage`, `ResponseBody`

**Potential:** Could enable partial-failure handling in batch operations. Currently `IODataBatchService` returns a single `Result` for the entire batch. With changeset inspection, we could return per-entity results and retry only failed items.

## GetCountAsync Returns long

```csharp
Task<long> GetCountAsync(ODataQueryBuilder<T> query, CancellationToken ct)
```

Returns `long` instead of `int`, future-proofing for datasets exceeding 2.1 billion records.

**Current usage:** We cast to `int` in `ODataClientAdapter.CountAsync`. If needed for very large D365 entity sets, expose `long` directly.

## String-Based OrderBy

```csharp
var query = client.For<Product>("Products")
    .OrderBy("Price desc");
```

Supports raw OData `$orderby` strings. The current `IODataService.QueryAsync` has an unused `orderBy` parameter of type `Func<IQueryable<T>, IOrderedQueryable<T>>` — this could be replaced with string-based ordering that maps directly to OData syntax.

## ConfigureRequest Callback

```csharp
var options = new ODataClientOptions
{
    ConfigureRequest = request =>
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Prefer", "return=representation");
    }
};
```

Per-request customisation callback. Could be used as an alternative to the `ODataAuthenticationHandler` DelegatingHandler for authentication. The current approach (DelegatingHandler via `IHttpClientFactory`) is more idiomatic for .NET DI and supports Polly policies, so the handler approach remains preferred.

## Cross-Join Queries

```csharp
var crossJoin = client.CrossJoin("Products", "Categories");
var result = await client.GetCrossJoinAsync(crossJoin, ct);
```

Enables OData cross-join queries across multiple entity sets. Not currently needed but available for complex reporting scenarios.

## Delta Queries

```csharp
var delta = await client.GetDeltaAsync(deltaLink, headers, ct);
```

Supports OData delta links for change tracking. Could enable incremental sync patterns between D365 F&O and RELion instead of full entity pulls.

## Raw Query Execution

```csharp
var json = await client.ExecuteRawQueryAsync("Products?$filter=Price gt 100", ct);
```

Escape hatch for queries not expressible through the typed builder. Useful for D365 F&O-specific OData extensions or custom query options.

---

## Migration Reference

| Before (Simple.OData.Client) | After (PanoramicData.OData.Client) |
|-------------------------------|-------------------------------------|
| `IODataClient` (interface) | `ODataClient` (concrete) + `IODataClientAdapter` (our wrapper) |
| `ODataClientSettings` | `ODataClientOptions` |
| `IBoundClient<T>` fluent chain | `ODataQueryBuilder<T>` |
| `ODataBatch` with `+=` operator | `ODataBatchBuilder` with `Changeset()` |
| `WebRequestException` | `ODataClientException` hierarchy |
| `MetadataDocument` property | Not used (metadata fetched on demand or skipped) |
| `AppContext.SetSwitch` for DTD | Removed (not needed) |
