# Batch Operations

```csharp
// CreateBatchCommand<TEntity> : ICommand<Result> — create multiple entities atomically
var journals = new List<LedgerJournalHeader>
{
    new() { DataAreaId = "USMF", JournalName = "GenJrn", Description = "Batch journal 1" },
    new() { DataAreaId = "USMF", JournalName = "GenJrn", Description = "Batch journal 2" },
    new() { DataAreaId = "USMF", JournalName = "GenJrn", Description = "Batch journal 3" }
};

Result result = await mediator.Send(
    new CreateBatchCommand<LedgerJournalHeader>(journals),
    cancellationToken); // result.IsSuccess == true; created entities are not returned
```

Batch commands return a non-generic `Result` — if you need server-generated values (e.g. `JournalBatchNumber`), [[Queries|query them]] after the batch completes.

## Update Batch

Every entity must have its [[Entities|composite key]] fully populated:

```csharp
// UpdateBatchCommand<TEntity> : ICommand<Result>
var updates = new List<LedgerJournalHeader>
{
    new() { DataAreaId = "USMF", JournalBatchNumber = "00628", JournalName = "GenJrn", Description = "Updated 1" },
    new() { DataAreaId = "USMF", JournalBatchNumber = "00629", JournalName = "GenJrn", Description = "Updated 2" }
};

Result result = await mediator.Send(
    new UpdateBatchCommand<LedgerJournalHeader>(updates),
    cancellationToken); // result.IsSuccess == true, or entire batch rolled back on failure
```

## Delete Batch

```csharp
// DeleteBatchCommand<TEntity> : ICommand<Result>
var deletes = new List<LedgerJournalHeader>
{
    new() { DataAreaId = "USMF", JournalBatchNumber = "00628", JournalName = "GenJrn" },
    new() { DataAreaId = "USMF", JournalBatchNumber = "00629", JournalName = "GenJrn" }
};

Result result = await mediator.Send(
    new DeleteBatchCommand<LedgerJournalHeader>(deletes),
    cancellationToken); // result.IsSuccess == true, or entire batch rolled back on failure
```

## Direct Service Access

Bypass the [[Extending-the-Pipeline|MediatR pipeline]] by injecting `IODataBatchService<T>` directly:

```csharp
Result result = await batchService.AddBatchAsync(journals, cancellationToken);
Result result = await batchService.UpdateBatchAsync(updates, cancellationToken);
Result result = await batchService.DeleteBatchAsync(deletes, cancellationToken);
```

## Chunking Large Batches

D365 F&O limits batch requests to ~5,000 operations. Split large datasets into chunks:

```csharp
const int batchSize = 1000;

foreach (IEnumerable<LedgerJournalHeader> chunk in journals.Chunk(batchSize))
{
    Result result = await mediator.Send(
        new CreateBatchCommand<LedgerJournalHeader>(chunk),
        cancellationToken);

    if (result.IsFailed)
        break; // previous chunks were committed; this one was not
}
```

Note: chunked batches are **not** atomic across chunks — earlier chunks commit independently, so a mid-way failure leaves partial data you must reconcile.

## See Also

- [[Commands]] — single-entity create, update, and delete
- [[D365-FO-Journals]] — journal-specific batch patterns
- [[Error-Handling]] — `Result<T>` pattern and error types
- [[Durable-Functions]] — orchestrate batches with fan-out/fan-in
