# Query Entities by Filter

Retrieve multiple entities from D365 F&O using LINQ filter expressions. The expression is translated into an OData `$filter` query string automatically.

> **Prerequisites:** [[Install-the-Framework]], [[Define-a-Custom-Entity]]

## Send a GetByFilterQuery via MediatR

```csharp
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;

Result<IEnumerable<LedgerJournalHeader>> result = await mediator.Send(
    new GetByFilterQuery<LedgerJournalHeader>(
        j => j.DataAreaId == "USMF" && j.JournalName == "GenJrn"),
    cancellationToken);
```

On success:

```
result.IsSuccess  = true
result.Value.Count()  = 12

// Each entity is fully populated:
result.Value.First().DataAreaId          = "USMF"
result.Value.First().JournalBatchNumber  = "00615"
result.Value.First().JournalName         = "GenJrn"
```

The LINQ expression `j => j.DataAreaId == "USMF" && j.JournalName == "GenJrn"` is translated to the OData query: `$filter=dataAreaId eq 'USMF' and JournalName eq 'GenJrn'`.

## Filter via Direct Service Call

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

// Inject IService<LedgerJournalHeader> via DI
Result<IEnumerable<LedgerJournalHeader>> result = await service.FindAsync(
    j => j.DataAreaId == "USMF" && j.JournalName == "GenJrn",
    cancellationToken);
// Result: Result<IEnumerable<LedgerJournalHeader>> — matching entities or empty collection

if (result.IsSuccess)
{
    foreach (LedgerJournalHeader journal in result.Value)
    {
        // Process each journal
    }
}
```

Pass `null` to `FindAsync` to retrieve all entities (use with caution on large data sets).

## Common Filter Patterns

**Equality:**

```csharp
j => j.DataAreaId == "USMF"
// $filter=dataAreaId eq 'USMF'
```

**Multiple conditions:**

```csharp
j => j.DataAreaId == "USMF" && j.JournalName == "GenJrn"
// $filter=dataAreaId eq 'USMF' and JournalName eq 'GenJrn'
```

**String contains:**

```csharp
j => j.Description.Contains("accrual")
// $filter=contains(Description, 'accrual')
```

**Enum comparison:**

```csharp
j => j.IsPosted == NoYes.Yes
// $filter=IsPosted eq 'Yes'
```

## Advanced Queries with IODataService

For paging, sorting, selecting, or expanding navigation properties, use `IODataService<T>.QueryAsync`:

```csharp
using IntegratoR.OData.Interfaces.Services;

// Inject IODataService<LedgerJournalHeader> via DI
Result<IEnumerable<LedgerJournalHeader>> result = await oDataService.QueryAsync(
    filter: j => j.DataAreaId == "USMF",
    orderBy: q => q.OrderByDescending(j => j.JournalBatchNumber),
    top: 10,
    skip: 0,
    cancellationToken: cancellationToken);
// Result: Result<IEnumerable<LedgerJournalHeader>> — up to 10 entities sorted by batch number descending
```

| Parameter | OData Equivalent | Description |
|-----------|-----------------|-------------|
| `filter` | `$filter` | LINQ expression for filtering |
| `orderBy` | `$orderby` | Sorting function |
| `expand` | `$expand` | Include navigation properties |
| `select` | `$select` | Return only specific fields |
| `skip` | `$skip` | Number of records to skip (paging) |
| `top` | `$top` | Maximum records to return |

## When Things Go Wrong

**Invalid filter expression** -- the LINQ expression cannot be translated to OData:

```
result.IsFailed  = true
result.GetError().Code     = "OData.Error"
result.GetError().Message  = "The query specified in the URI is not valid."
result.GetError().Type     = ErrorType.Failure
```

**Empty result set** -- no entities match the filter. This is not an error:

```
result.IsSuccess  = true
result.Value.Count()  = 0
```

## Avoid Common Pitfalls

- **Avoid unfiltered queries on large entity sets** -- passing `null` or very broad filters can return thousands of records and time out against D365 F&O.
- **Use `$top` and `$skip` for pagination** when working with large datasets; the `IODataService<T>.QueryAsync` method supports both parameters.
- **Complex filter expressions may not be supported** by all D365 entities -- if you receive "query not valid" errors, simplify the LINQ expression or split it into multiple queries.

## See Also

- [[Query-Entities-by-Key]] — query a single entity by its composite key
- [[Cache-Query-Results]] — cache frequently used filter queries
- [[Handle-Errors-with-Result]] — handle failures from filter queries
