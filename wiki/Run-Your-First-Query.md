# Run Your First Query

Retrieve entities from D365 F&O by composite key or filter expression. This page assumes you have [[Register-Services-in-Your-Host|registered services]] and [[Create-Your-First-Entity|defined an entity]].

## Query by Composite Key

```csharp
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.Abstractions.Common.Results;
using MediatR;

var query = new GetByKeyQuery<LedgerJournalHeader>(["USMF", "JBN-000431"]);
Result<LedgerJournalHeader> result = await mediator.Send(query, cancellationToken);

if (result.IsSuccess)
{
    LedgerJournalHeader header = result.Value;
    Console.WriteLine($"Journal: {header.JournalBatchNumber}, Name: {header.JournalName}");
    // Output: Journal: JBN-000431, Name: GenJrn
}
```

The key values are passed as an `object[]` in the same order as `GetCompositeKey()` returns them -- `DataAreaId` first, then `JournalBatchNumber`.

## Query by Filter

```csharp
var query = new GetByFilterQuery<LedgerJournalHeader>(
    h => h.DataAreaId == "USMF" && h.JournalName == "GenJrn");

Result<IEnumerable<LedgerJournalHeader>> result = await mediator.Send(query, cancellationToken);

if (result.IsSuccess)
{
    foreach (LedgerJournalHeader header in result.Value)
    {
        Console.WriteLine($"  {header.JournalBatchNumber}: {header.Description}");
    }
    // Output:
    //   JBN-000431: Monthly accruals - March 2026
    //   JBN-000432: Monthly accruals - April 2026
}
```

The LINQ expression is translated into an OData `$filter` query parameter automatically. Use standard comparison operators (`==`, `!=`, `>`, `<`, `>=`, `<=`) and logical operators (`&&`, `||`).

## Handling Not Found

A key query that finds no matching entity returns a failed result:

```csharp
var query = new GetByKeyQuery<LedgerJournalHeader>(["USMF", "DOES-NOT-EXIST"]);
Result<LedgerJournalHeader> result = await mediator.Send(query, cancellationToken);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    Console.WriteLine($"[{error?.Code}] {error?.Message} (Type: {error?.Type})");
    // Output: [NOT_FOUND] Entity not found (Type: NotFound)
}
```

A filter query that matches no entities returns a successful result with an empty collection:

```csharp
var query = new GetByFilterQuery<LedgerJournalHeader>(
    h => h.DataAreaId == "NONEXISTENT");

Result<IEnumerable<LedgerJournalHeader>> result = await mediator.Send(query, cancellationToken);
// result.IsSuccess == true
// result.Value is empty IEnumerable
```

## Using Match

```csharp
var query = new GetByKeyQuery<LedgerJournalHeader>(["USMF", "JBN-000431"]);
Result<LedgerJournalHeader> result = await mediator.Send(query, cancellationToken);

string output = result.Match(
    onSuccess: header => $"Found: {header.JournalBatchNumber} - {header.Description}",
    onFailure: error => $"Error: [{error.Code}] {error.Message}");

Console.WriteLine(output);
// Success output: Found: JBN-000431 - Monthly accruals - March 2026
// Failure output: Error: [NOT_FOUND] Entity not found
```

## Return Types

| Query | Returns |
|-------|---------|
| `GetByKeyQuery<TEntity>` | `Result<TEntity>` -- single entity or failure |
| `GetByFilterQuery<TEntity>` | `Result<IEnumerable<TEntity>>` -- collection (may be empty) or failure |

## What Just Happened

- `GetByKeyQuery` translates the composite key array into an OData key predicate (e.g. `LedgerJournalHeaders(dataAreaId='USMF',JournalBatchNumber='JBN-000431')`).
- `GetByFilterQuery` translates the LINQ expression into an OData `$filter` parameter.
- Both queries pass through the MediatR pipeline (Logging, Validation, Caching) before reaching the handler.
- Results are always wrapped in `Result<T>` -- use `IsSuccess`, `IsFailed`, `GetError()`, or `Match` to inspect them.

## See Also

- [[Send-Your-First-Command]] — send a write command through MediatR
- [[Create-Your-First-Entity]] — define the entity class to query
- [[Configure-the-OData-Connection]] — set up the OData connection first
