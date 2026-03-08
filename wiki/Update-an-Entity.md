# Update an Entity

Modify an existing D365 F&O entity via OData PATCH. The entity must have its composite key fully populated so the OData client can construct the correct URL.

> **Prerequisites:** [[Install-the-Framework]], [[Define-a-Custom-Entity]]

## Send an Update Command via MediatR

```csharp
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;

// Entity must have all [Key] properties populated
var journal = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalBatchNumber = "00628",
    JournalName = "GenJrn",
    Description = "Monthly accruals - March 2026 (amended)"
};

Result<LedgerJournalHeader> result = await mediator.Send(
    new UpdateCommand<LedgerJournalHeader>(journal),
    cancellationToken);
```

On success:

```
result.IsSuccess  = true
result.Value.Description  = "Monthly accruals - March 2026 (amended)"
```

The OData client sends a PATCH request to `LedgerJournalHeaders(dataAreaId='USMF',JournalBatchNumber='00628')`. Only modifiable properties are included in the payload -- properties marked `[ODataField(IgnoreOnUpdate = true)]` are excluded automatically.

## Update via Direct Service Call

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

Result<LedgerJournalHeader> result = await service.UpdateAsync(journal, cancellationToken);
// Result: Result<LedgerJournalHeader> — Success with updated entity

if (result.IsSuccess)
{
    LedgerJournalHeader updated = result.Value;
}
```

## Composite Key Requirements

Both `[Key]` properties must be set. `GetCompositeKey()` constructs the key array used to build the OData URL:

```csharp
// LedgerJournalHeader.GetCompositeKey() returns:
// [ "USMF", "00628" ]
```

If `JournalBatchNumber` is null or missing, the OData client cannot resolve the entity URL and the operation will fail.

## When Things Go Wrong

**Entity not found** -- the composite key does not match any record in D365:

```
result.IsFailed  = true
result.GetError().Code     = "OData.Error"
result.GetError().Message  = "Resource not found for the segment 'LedgerJournalHeaders'."
result.GetError().Type     = ErrorType.NotFound
```

**Validation failure** -- a registered validator rejects the command:

```
result.IsFailed  = true
result.GetError().Code     = "Validation.Error"
result.GetError().Message  = "'Description' must not be empty."
result.GetError().Type     = ErrorType.Validation
```

## See Also

- [[Create-an-Entity]] — create entities before updating them
- [[Delete-an-Entity]] — remove entities that are no longer needed
- [[Handle-Errors-with-Result]] — inspect failures from update operations
- [[Define-a-Custom-Entity]] — define the entity class with ODataFieldAttribute
