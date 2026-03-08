# Delete an Entity

Remove an entity from D365 F&O via OData DELETE. The entity must have its composite key populated to resolve the OData URL.

> **Prerequisites:** [[Install-the-Framework]], [[Define-a-Custom-Entity]]

## Send a Delete Command via MediatR

```csharp
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;

var journal = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalBatchNumber = "00628",
    JournalName = "GenJrn",
    Description = "To be deleted"
};

Result<LedgerJournalHeader> result = await mediator.Send(
    new DeleteCommand<LedgerJournalHeader>(journal),
    cancellationToken);
```

On success:

```
result.IsSuccess  = true
```

The OData client sends a DELETE request to `LedgerJournalHeaders(dataAreaId='USMF',JournalBatchNumber='00628')`.

## Delete via Direct Service Call

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

Result result = await service.DeleteAsync(journal, cancellationToken);
// Result: Result — Success (even if entity was already deleted)

if (result.IsSuccess)
{
    // Entity has been removed from D365
}
```

Note that `IService<T>.DeleteAsync` returns a non-generic `Result` (no entity value returned).

## D365 Quirk: Idempotent Deletes

D365 F&O OData treats deleting a non-existent entity as a successful operation (HTTP 204 No Content). The ODataService follows this convention -- if the entity has already been deleted, the result is still `IsSuccess = true`. This makes delete operations naturally idempotent, which is useful for retry scenarios in integrations.

## When Things Go Wrong

**Missing composite key** -- if a `[Key]` property is null, the OData URL cannot be constructed:

```
result.IsFailed  = true
result.GetError().Code     = "OData.Error"
result.GetError().Message  = "Key value cannot be null for entity 'LedgerJournalHeader'."
result.GetError().Type     = ErrorType.Failure
```

**Posted journal** -- D365 business rules may prevent deletion of certain records:

```
result.IsFailed  = true
result.GetError().Code     = "OData.Error"
result.GetError().Message  = "Journal '00628' has been posted and cannot be deleted."
result.GetError().Type     = ErrorType.Conflict
```

## See Also

- [[Create-an-Entity]] — create entities before deleting them
- [[Update-an-Entity]] — update as an alternative to delete
- [[Batch-Multiple-Operations]] — delete multiple entities in a single batch
- [[Handle-Errors-with-Result]] — inspect failures from delete operations
