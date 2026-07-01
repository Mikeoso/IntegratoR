# Run Queries

> Last verified against v2.0.1

Read records from D365 F&O with two generic queries — by composite key and by LINQ filter — sent through `IMediator`. Both return a `Result<T>`; neither mutates the server. For `$orderby`, `$select`, `$expand`, paging, and `$count`, drop to the typed `IODataService<TEntity>` methods.

```csharp
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

// Read one LedgerJournalHeader by its composite key [DataAreaId, JournalBatchNumber].
Result<LedgerJournalHeader> result = await mediator.Send(
    new GetByKeyQuery<LedgerJournalHeader>(["USMF", "B0001"]),
    cancellationToken).ConfigureAwait(false);

if (result.IsSuccess)
{
    LedgerJournalHeader header = result.Value;
}
```

`GetByKeyQuery<TEntity>(object[] CompositeKey)` is a `record` whose single positional parameter carries the key values in the order `TEntity.GetCompositeKey()` returns them. For `LedgerJournalHeader` that order is `[DataAreaId, JournalBatchNumber]`; for `LedgerJournalLine` it is `[DataAreaId, JournalBatchNumber, LineNumber]`. Read the entity source before you build a key — see [Define Entities](Define-Entities).

## Handle the failure path

A missing key is a failed `Result`, not an exception — the never-throw model holds for reads too. Branch on `result.IsFailed` and read the `IntegrationError` off `result.GetError()`.

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(
    new GetByKeyQuery<LedgerJournalHeader>(["USMF", "does-not-exist"]),
    cancellationToken).ConfigureAwait(false);

if (result.IsFailed)
{
    IntegrationError error = (IntegrationError)result.GetError();
    // error.Type == ErrorType.NotFound for a key D365 has no record for.
    // error.Code and error.Message carry the machine-readable detail.
}
```

`ErrorType` has four members: `Failure`, `Validation`, `NotFound`, `Conflict`. A key lookup that D365 cannot resolve fails with `ErrorType.NotFound`. See [Handle Errors](Handle-Errors) for the full mapping.

## Filter with a LINQ expression

`GetByFilterQuery<TEntity>(Expression<Func<TEntity, bool>> Filter)` takes a LINQ expression tree and returns `Result<IEnumerable<TEntity>>`. Write strongly-typed predicates — never raw OData filter strings.

```csharp
Result<IEnumerable<LedgerJournalHeader>> result = await mediator.Send(
    new GetByFilterQuery<LedgerJournalHeader>(
        h => h.DataAreaId == "USMF"
          && h.JournalName == "GenJrn"
          && h.IsPosted == NoYes.No),
    cancellationToken).ConfigureAwait(false);

if (result.IsFailed)
{
    IntegrationError error = (IntegrationError)result.GetError();
}
else
{
    foreach (LedgerJournalHeader header in result.Value)
    {
        // Process each matching journal.
    }
}
```

A filter with no matches is a **successful** `Result` carrying an empty collection — not a `NotFound` failure. Only a transport or translation problem produces a failed `Result` here.

## What the translator emits

The predicate above translates to this D365-compatible `$filter`:

```
$filter=dataAreaId eq 'USMF' and JournalName eq 'GenJrn' and IsPosted eq Microsoft.Dynamics.DataEntities.NoYes'No'
```

Three translator behaviours matter for D365 work:

- **`[JsonPropertyName]` is honoured** across filter, select, expand, and orderby. The PascalCase CLR property `DataAreaId` emits its camelCase wire name `dataAreaId`, so a legacy X++ field sorts and filters correctly.
- **Enum constants emit the qualified-type form.** `h.IsPosted == NoYes.No` emits `IsPosted eq Microsoft.Dynamics.DataEntities.NoYes'No'`, not the integer `1` that D365 F&O OData v4 rejects. This covers both top-level predicates and `Any`/`All` lambda bodies.
- **`&&` and `||`** preserve operator precedence with parenthesisation.

> [!NOTE]
> The translator is a copy-and-patch fork of PanoramicData.OData.Client's parser, because the upstream reads `MemberInfo.Name` and ignores `[JsonPropertyName]`. It is intentionally narrow: a LINQ shape outside the matrix below throws `NotSupportedException` at translation time, favouring predictable D365 output over full LINQ coverage.

### Supported filter shapes

| LINQ shape | Emitted OData |
|---|---|
| `h => h.Prop == "x"` | `Prop eq 'x'` |
| `h => h.Prop != "x"` | `Prop ne 'x'` |
| `h => h.IsPosted == NoYes.Yes` | `IsPosted eq Microsoft.Dynamics.DataEntities.NoYes'Yes'` |
| `h => h.Amount > 100m` | `Amount gt 100` |
| `h => h.A == "x" && h.B == "y"` | `A eq 'x' and B eq 'y'` |
| `h => h.A == "x" \|\| h.B == "y"` | `A eq 'x' or B eq 'y'` |
| `h => h.A.StartsWith("X")` | `startswith(A, 'X')` |
| `h => h.A.Contains("X")` | `contains(A, 'X')` |
| `h => string.IsNullOrEmpty(h.A)` | `(A eq null or A eq '')` |
| `h => h.Lines.Any(l => l.X == "y")` | `Lines/any(l: l/X eq 'y')` |
| `h => h.Lines.All(l => l.X == "y")` | `Lines/all(l: l/X eq 'y')` |
| `h => collection.Contains(h.A)` | `A in ('a','b','c')` |

## Order, page, and count with IODataService

`GetByFilterQuery` covers a flat filtered read. For `$orderby`, `$select`, `$expand`, and paging, resolve `IODataService<TEntity>` and call `QueryAsync` from your own handler. Use the typed `orderBy` overload — it takes an ordered list of `(KeySelector, Descending)` tuples and honours `[JsonPropertyName]` on each key selector.

```csharp
Result<IEnumerable<LedgerJournalHeader>> result = await service.QueryAsync(
    filter: h => h.DataAreaId == "USMF" && h.IsPosted == NoYes.No,
    orderBy: [(h => h.JournalBatchNumber, Descending: true)],
    expand: null,
    select: null,
    skip: 0,
    top: 50,
    cancellationToken: cancellationToken).ConfigureAwait(false);

if (result.IsFailed)
{
    IntegrationError error = (IntegrationError)result.GetError();
}
```

> [!WARNING]
> Do not use the older `QueryAsync` overload whose `orderBy` is a `Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>` — it is `[Obsolete]` and **silently drops** the ordering (the sort never reaches the OData query). Pass the `IReadOnlyList<(Expression<Func<TEntity, object>> KeySelector, bool Descending)>` overload instead.

Count matching records server-side with `CountAsync`, which emits `$count` and returns only the integer — no rows cross the wire.

```csharp
Result<int> count = await service.CountAsync(
    h => h.DataAreaId == "USMF" && h.IsPosted == NoYes.No,
    cancellationToken).ConfigureAwait(false);

int openJournals = count.IsSuccess ? count.Value : 0;
```

## Cache a slow read

For data that changes rarely — dimension formats, reference data — make the query implement `ICacheableQuery<TResponse>`. Supply `CacheKey` and `CacheDuration`; the `CachingBehaviour` reads and writes the cache around the handler.

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Queries;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;

public record GetDimensionOrdersQuery(string DimensionFormat, DimensionHierarchyType HierarchyType)
    : ICacheableQuery<Result<DimensionFormat>>
{
    public string CacheKey => $"{nameof(GetDimensionOrdersQuery)}-{DimensionFormat}-{HierarchyType}";

    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(15);

    public IReadOnlyDictionary<string, object> GetLoggingContext() => new Dictionary<string, object>
    {
        { "DimensionFormat", DimensionFormat },
        { "HierarchyType", HierarchyType.ToString() }
    };
}
```

The `CachingBehaviour` returns a cached `Result<T>` on a `CacheKey` hit and otherwise runs the handler, caching only a successful result for `CacheDuration` — failed results are never cached, so the next call retries. Set `CacheDuration` to `null` on an instance to skip the cache even when the type opts in. See [Cache Query Results](Cache-Query-Results) for distributed-cache wiring.

> [!NOTE]
> `CacheKey` is the only key member you implement. The `GenerateCacheKey()` and `GetCacheKeyValues()` members on `ICacheableQuery<T>` are `[Obsolete]` — the behaviour uses `CacheKey` directly and both are removed in the next MAJOR.

## Compose a custom query

When the generic shapes are not enough — composing two service calls, or applying a domain projection — define a `record` implementing `IQuery<TResponse>` and a handler.

```csharp
public record GetOpenJournalCountQuery(string DataAreaId) : IQuery<Result<int>>;

public sealed class GetOpenJournalCountQueryHandler
    : IRequestHandler<GetOpenJournalCountQuery, Result<int>>
{
    private readonly IService<LedgerJournalHeader> _service;

    public GetOpenJournalCountQueryHandler(IService<LedgerJournalHeader> service) => _service = service;

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

Register the handler's assembly through `AddIntegratoR` with `AddConsumerHandlers(...)` in the configure delegate — that closes your generic handlers and validators over the framework's. See [Send Commands](Send-Commands).

## Return types at a glance

| Query | Return type | Empty / missing |
|---|---|---|
| `GetByKeyQuery<TEntity>` | `Result<TEntity>` | Missing key → `Result.Fail` with `ErrorType.NotFound` |
| `GetByFilterQuery<TEntity>` | `Result<IEnumerable<TEntity>>` | No matches → `Result.Ok` with an empty collection |
| `IODataService<T>.CountAsync` | `Result<int>` | No matches → `Result.Ok(0)` |

## See Also

- [Define Entities](Define-Entities)
- [Send Commands](Send-Commands)
- [Handle Errors](Handle-Errors)
- [Cache Query Results](Cache-Query-Results)
