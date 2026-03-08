# Query Entities by Key

Retrieve a single entity from D365 F&O using its composite key. D365 entities commonly use composite keys combining `DataAreaId` with one or more business keys.

> **Prerequisites:** [[Install-the-Framework]], [[Define-a-Custom-Entity]]

## Send a GetByKeyQuery via MediatR

```csharp
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;

// Construct the composite key as an object array
var compositeKey = new object[] { "USMF", "00628" };

Result<LedgerJournalHeader> result = await mediator.Send(
    new GetByKeyQuery<LedgerJournalHeader>(compositeKey),
    cancellationToken);
```

On success:

```
result.IsSuccess  = true
result.Value.DataAreaId          = "USMF"
result.Value.JournalBatchNumber  = "00628"
result.Value.JournalName         = "GenJrn"
result.Value.Description         = "Monthly accruals - March 2026"
```

The OData client translates this to a GET request: `LedgerJournalHeaders(dataAreaId='USMF',JournalBatchNumber='00628')`.

## Query by Key via Direct Service Call

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

// Inject IService<LedgerJournalHeader> via DI
var compositeKey = new object[] { "USMF", "00628" };

Result<LedgerJournalHeader> result = await service.GetByKeyAsync(
    compositeKey,
    cancellationToken);
// Result: Result<LedgerJournalHeader> — the matching entity or failure if not found

if (result.IsSuccess)
{
    LedgerJournalHeader journal = result.Value;
}
```

## Constructing Composite Keys

The key array order must match the order of `[Key]` attributes on the entity class. For `LedgerJournalHeader`:

```csharp
[Key]
[JsonPropertyName("dataAreaId")]
public required string DataAreaId { get; set; }     // index 0

[Key]
[JsonPropertyName("JournalBatchNumber")]
public string? JournalBatchNumber { get; set; }     // index 1
```

So the key array is always `new object[] { DataAreaId, JournalBatchNumber }`.

You can also use `GetCompositeKey()` on an existing entity instance:

```csharp
var existingJournal = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalBatchNumber = "00628",
    JournalName = "GenJrn",
    Description = "Example"
};

object[] key = existingJournal.GetCompositeKey();
// Result: [ "USMF", "00628" ]
```

## D365 Quirk: Key Order Matters

The order of values in the `object[]` must match the order of `[Key]` attributes as they appear on the entity class. If you swap `DataAreaId` and `JournalBatchNumber`, the OData URL will contain incorrect key segments and the query will return a `NotFound` error or the wrong entity.

## When Things Go Wrong

**Entity not found** -- no record matches the composite key:

```
result.IsFailed  = true
result.GetError().Code     = "OData.Error"
result.GetError().Message  = "Resource not found for the segment 'LedgerJournalHeaders'."
result.GetError().Type     = ErrorType.NotFound
```

**Incorrect key order** -- key values are swapped or incomplete:

```
result.IsFailed  = true
result.GetError().Code     = "OData.Error"
result.GetError().Message  = "Resource not found for the segment 'LedgerJournalHeaders'."
result.GetError().Type     = ErrorType.NotFound
```

## See Also

- [[Query-Entities-by-Filter]] — query multiple entities with a filter expression
- [[Define-a-Custom-Entity]] — define the entity and composite key to query
- [[Handle-Errors-with-Result]] — handle not-found and other query failures
