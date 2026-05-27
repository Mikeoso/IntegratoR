# Run Queries

Queries retrieve records from D365 F&O without modifying them. The framework provides two generic query types — by composite key and by LINQ filter expression — plus an opt-in caching layer for queries that hit slow or rarely-changing data.

## Get a Record by Key

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(
    new GetByKeyQuery<LedgerJournalHeader>(["USMF", "JBN-000431"]),
    cancellationToken).ConfigureAwait(false);

if (result.IsSuccess)
{
    LedgerJournalHeader header = result.Value;
}
```

`GetByKeyQuery<TEntity>(object[] CompositeKey)` is a `record` with a single positional parameter — the key values in the same order returned by `TEntity.GetCompositeKey()`. For `LedgerJournalHeader` the order is `[DataAreaId, JournalBatchNumber]`; for `LedgerJournalLine` it is `[DataAreaId, JournalBatchNumber, LineNumber]`.

The framework builds an OData URL of the form `<Url>/<TableName>(field1='value1',field2='value2')`. Because D365 entity sets often use composite keys with mixed CLR types, the values are coerced to their OData literal form (strings quoted, integers bare, decimals with `M` suffix, dates as `datetimeoffset`).

> The composite-key read path uses a `$filter`-based bypass internally rather than the OData `(key=value, ...)` segment, because PanoramicData.OData.Client lacks a composite-key write API. Reads work transparently; writes have a [Known Limitation](Known-Limitations#composite-key-writes).

## Filter Records

```csharp
Result<IEnumerable<LedgerJournalHeader>> result = await mediator.Send(
    new GetByFilterQuery<LedgerJournalHeader>(
        h => h.DataAreaId == "USMF"
          && h.JournalName == "GenJrn"
          && h.IsPosted == NoYes.No),
    cancellationToken).ConfigureAwait(false);

if (result.IsSuccess)
{
    foreach (LedgerJournalHeader header in result.Value)
    {
        // Process each matching journal
    }
}
```

`GetByFilterQuery<TEntity>(Expression<Func<TEntity, bool>> Filter)` takes a LINQ expression tree. The framework's translator emits the D365-compatible OData `$filter` query parameter:

```
$filter=dataAreaId eq 'USMF' and JournalName eq 'GenJrn' and IsPosted eq Microsoft.Dynamics.DataEntities.NoYes'No'
```

Three translator capabilities matter for D365 work:

- **`[JsonPropertyName]` honoured** — PascalCase CLR `DataAreaId` emits camelCase wire `dataAreaId` (since v1.3.3).
- **Enum constants emit the qualified-type form** — `h.IsPosted == NoYes.No` emits `IsPosted eq Microsoft.Dynamics.DataEntities.NoYes'No'`, not the integer form D365 rejects (since v1.3.5; covers both top-level predicates and `Any`/`All` lambda bodies).
- **Composite predicates with `&&` and `||`** preserve operator precedence with appropriate parenthesisation.

## Supported Filter Shapes

| LINQ shape | Emitted OData |
|---|---|
| `h => h.Prop == "x"` | `Prop eq 'x'` |
| `h => h.Prop != "x"` | `Prop ne 'x'` |
| `h => h.IsActive == NoYes.Yes` | `IsActive eq Microsoft.Dynamics.DataEntities.NoYes'Yes'` |
| `h => h.Amount > 100m` | `Amount gt 100M` |
| `h => h.A == "x" && h.B == "y"` | `A eq 'x' and B eq 'y'` |
| `h => h.A == "x' \|\| h.B == "y"` | `A eq 'x' or B eq 'y'` |
| `h => h.A.StartsWith("X")` | `startswith(A, 'X')` |
| `h => h.A.Contains("X")` | `contains(A, 'X')` |
| `h => string.IsNullOrEmpty(h.A)` | `(A eq null or A eq '')` |
| `h => h.Lines.Any(l => l.X == "y")` | `Lines/any(l: l/X eq 'y')` |
| `h => h.Lines.All(l => l.X == "y")` | `Lines/all(l: l/X eq 'y')` |
| `h => h.Lines.Any()` | `Lines/any()` |
| `h => collection.Contains(h.A)` | `A in ('a','b','c')` |

Complex LINQ shapes that fall outside this matrix may throw `NotSupportedException` at translation time. The translator is intentionally narrow — it favours predictable D365-compatible output over comprehensive LINQ coverage.

## Cache Query Results

For data that changes infrequently (configuration metadata, dimension formats, reference data), make the query implement `ICacheableQuery<TResponse>`:

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Queries;

public record GetDimensionOrdersQuery(
    string dimensionFormat,
    DimensionHierarchyType hierarchyType)
    : IQuery<Result<DimensionFormat>>, ICacheableQuery<Result<DimensionFormat>>
{
    public string CacheKey =>
        $"GetDimensionOrdersQuery-{dimensionFormat}-{hierarchyType}";

    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(15);
}
```

The `CachingBehaviour` in the MediatR pipeline:

1. Checks the cache for an entry matching `CacheKey`. If present and not expired, returns the cached `Result<T>` without calling the handler.
2. If absent, calls the handler. If the result is successful, caches it for `CacheDuration`. Failed results are **never** cached — the next call retries.

Set `CacheDuration` to `null` on a per-instance basis to bypass caching even when the query type opts in. See [Cache Query Results](Cache-Query-Results) for distributed-cache wiring, eviction patterns, and cache-key best practices.

## Pipeline Flow for Queries

Identical to commands, with one branch:

1. **`LoggingBehaviour`** — request type, duration, structured context.
2. **`ValidationBehaviour`** — registered validators run for the query type.
3. **`CachingBehaviour`** — returns cache hit when the query implements `ICacheableQuery`.
4. **Handler** — calls `ODataService<TEntity>.FindByKeyAsync` / `FindAsync` / `FindAll` depending on the query type.

## Custom Queries

When the generic `GetByKeyQuery` / `GetByFilterQuery` shape is not enough — for example, a query that needs to compose two service calls or apply a domain projection — define a `record` that implements `IQuery<TResponse>`:

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Queries;

public record GetOpenJournalCountQuery(string DataAreaId)
    : IQuery<Result<int>>;
```

Pair with a handler implementing `IRequestHandler<GetOpenJournalCountQuery, Result<int>>`:

```csharp
public sealed class GetOpenJournalCountQueryHandler
    : IRequestHandler<GetOpenJournalCountQuery, Result<int>>
{
    private readonly IService<LedgerJournalHeader> _service;

    public GetOpenJournalCountQueryHandler(IService<LedgerJournalHeader> service)
    {
        _service = service;
    }

    public async Task<Result<int>> Handle(GetOpenJournalCountQuery request, CancellationToken cancellationToken)
    {
        Result<IEnumerable<LedgerJournalHeader>> result = await _service.FindAsync(
            h => h.DataAreaId == request.DataAreaId && h.IsPosted == NoYes.No,
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Result.Ok(result.Value.Count())
            : Result.Fail<int>(result.Errors);
    }
}
```

Register the handler's assembly via `AddConsumerHandlers(...)`. Make the query implement `ICacheableQuery<TResponse>` if it makes sense to cache.

## Return Types

| Query | Return type |
|---|---|
| `GetByKeyQuery<TEntity>` | `Result<TEntity>` — `Value` is the single matching entity |
| `GetByFilterQuery<TEntity>` | `Result<IEnumerable<TEntity>>` — `Value` is the matched collection (possibly empty) |

A `GetByKeyQuery` for a non-existent key returns `Result.Fail` with `ErrorType.NotFound`. A `GetByFilterQuery` with no matches returns `Result.Ok(Enumerable.Empty<TEntity>())` — empty matches are a successful query.

## See Also

- [Define Entities](Define-Entities) — composite key shape and `[JsonPropertyName]` translator behaviour
- [Cache Query Results](Cache-Query-Results) — `ICacheableQuery`, in-memory vs distributed cache
- [Handle Errors](Handle-Errors) — `Result<T>` and `IntegrationError`
- [Send Commands](Send-Commands) — modify the records this query returned
- [Work with Dimensions](Work-with-Dimensions) — `GetDimensionOrdersQuery` end-to-end example
