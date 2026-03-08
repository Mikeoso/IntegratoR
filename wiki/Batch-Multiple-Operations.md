# Batch Multiple Operations

Execute multiple create, update, or delete operations in a single atomic OData `$batch` request. Batch operations are all-or-nothing -- if one operation fails, the entire batch is rolled back.

> **Prerequisites:** [[Install-the-Framework]], [[Define-a-Custom-Entity]]

## Create a Batch of Entities

```csharp
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;

var journals = new List<LedgerJournalHeader>
{
    new()
    {
        DataAreaId = "USMF",
        JournalName = "GenJrn",
        Description = "Batch journal 1"
    },
    new()
    {
        DataAreaId = "USMF",
        JournalName = "GenJrn",
        Description = "Batch journal 2"
    },
    new()
    {
        DataAreaId = "USMF",
        JournalName = "GenJrn",
        Description = "Batch journal 3"
    }
};

Result result = await mediator.Send(
    new CreateBatchCommand<LedgerJournalHeader>(journals),
    cancellationToken);
```

On success:

```
result.IsSuccess  = true
```

Batch commands return a non-generic `Result` -- the created entities are not returned. If you need the server-generated values (e.g. `JournalBatchNumber`), query them after the batch completes.

## Update a Batch of Entities

Every entity in the batch must have its composite key fully populated:

```csharp
var updates = new List<LedgerJournalHeader>
{
    new()
    {
        DataAreaId = "USMF",
        JournalBatchNumber = "00628",
        JournalName = "GenJrn",
        Description = "Updated description 1"
    },
    new()
    {
        DataAreaId = "USMF",
        JournalBatchNumber = "00629",
        JournalName = "GenJrn",
        Description = "Updated description 2"
    }
};

Result result = await mediator.Send(
    new UpdateBatchCommand<LedgerJournalHeader>(updates),
    cancellationToken);
// Result: Result — Success if all updates applied, or Failure (entire batch rolled back)
```

## Delete a Batch of Entities

```csharp
var deletes = new List<LedgerJournalHeader>
{
    new()
    {
        DataAreaId = "USMF",
        JournalBatchNumber = "00628",
        JournalName = "GenJrn",
        Description = "To delete"
    },
    new()
    {
        DataAreaId = "USMF",
        JournalBatchNumber = "00629",
        JournalName = "GenJrn",
        Description = "To delete"
    }
};

Result result = await mediator.Send(
    new DeleteBatchCommand<LedgerJournalHeader>(deletes),
    cancellationToken);
// Result: Result — Success if all deletes applied, or Failure (entire batch rolled back)
```

## Use the Batch Service Directly

For direct access without the MediatR pipeline, inject `IODataBatchService<T>`:

```csharp
using FluentResults;
using IntegratoR.OData.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

// Inject IODataBatchService<LedgerJournalHeader> via DI
Result result = await batchService.AddBatchAsync(journals, cancellationToken);
// Result: Result — Success or Failure (atomic, all-or-nothing)

Result result = await batchService.UpdateBatchAsync(updates, cancellationToken);
// Result: Result — Success or Failure (atomic, all-or-nothing)

Result result = await batchService.DeleteBatchAsync(deletes, cancellationToken);
// Result: Result — Success or Failure (atomic, all-or-nothing)
```

## D365 Quirk: Batch Size Limits

D365 F&O imposes a limit on the number of operations per batch request. The default limit is typically around **5,000 operations per batch**. If you exceed this, D365 returns a 400 error. For large data sets, split your entities into chunks before sending:

```csharp
const int batchSize = 1000;

foreach (IEnumerable<LedgerJournalHeader> chunk in journals.Chunk(batchSize))
{
    Result result = await mediator.Send(
        new CreateBatchCommand<LedgerJournalHeader>(chunk),
        cancellationToken);

    if (result.IsFailed)
    {
        // Handle failure -- previous chunks were committed, this one was not
        break;
    }
}
```

## When Things Go Wrong

**Atomic failure** -- if any operation in the batch fails, the entire batch is rolled back:

```
result.IsFailed  = true
result.GetError().Code     = "OData.Error"
result.GetError().Message  = "Batch request failed: Journal name 'INVALID' does not exist."
result.GetError().Type     = ErrorType.Failure
```

**Batch size exceeded**:

```
result.IsFailed  = true
result.GetError().Code     = "OData.Error"
result.GetError().Message  = "The batch request contains too many operations."
result.GetError().Type     = ErrorType.Failure
```

## Avoid Common Pitfalls

- **D365 F&O limits batch request sizes** -- exceeding the limit (typically ~5,000 operations) returns a 400 error, so always chunk large datasets into smaller batches.
- **A single failure rolls back the entire batch** -- if one operation is invalid, every operation in that batch is lost, so validate entities before submitting.
- **Chunked batches are not atomic across chunks** -- earlier chunks commit independently, meaning a failure mid-way leaves partial data that you must handle or reconcile.

## See Also

- [[Create-an-Entity]] — single-entity create before batching
- [[Update-an-Entity]] — single-entity update before batching
- [[Delete-an-Entity]] — single-entity delete before batching
- [[Handle-Errors-with-Result]] — inspect partial failures in batch results
