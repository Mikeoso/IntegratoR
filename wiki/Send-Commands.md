# Send Commands
> Last verified against v3.0.0

Commands modify D365 F&O state through OData create (POST), update (PATCH), and delete (DELETE) requests. Send them with `IMediator.Send`; each returns a `Result<T>` (single) or `Result<BatchOutcome>` (batch) — no command throws for a business failure.

```csharp
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;

LedgerJournalHeader header = new()
{
    DataAreaId = "USMF",
    JournalName = "GenJrn",
    Description = "Monthly accruals",
};

Result<LedgerJournalHeader> result = await mediator
    .Send(new CreateCommand<LedgerJournalHeader>(header), cancellationToken)
    .ConfigureAwait(false);

if (result.IsSuccess)
{
    // D365 assigns the batch number from a number sequence on create.
    string batchNumber = result.Value.JournalBatchNumber!;
}
```

`CreateCommand<TEntity>` strips every `[ODataField(IgnoreOnCreate = true)]` property (here `JournalBatchNumber`) before the POST, serialises the rest through each property's `[JsonPropertyName]` mapping, and deserialises the D365 response into a fresh entity with server-assigned fields populated. See [Define Entities](Define-Entities) for the attribute rules.

## Update a record

Update sends a PATCH keyed on the entity's composite key. The entity must carry a fully populated key — the handler reads `GetCompositeKey()` to build the URL segment.

```csharp
header.Description = "Updated description";

Result<LedgerJournalHeader> result = await mediator
    .Send(new UpdateCommand<LedgerJournalHeader>(header), cancellationToken)
    .ConfigureAwait(false);
```

Composite-key Update and Delete are live (since v2.0.0). When the adapter sees a composite key it issues the write through an owned raw-`HttpClient` bypass that builds the keyed URL by hand — `LedgerJournalHeaders(dataAreaId='USMF',JournalBatchNumber='B0001')` — over the named `"ODataClient"`, so the write carries the same authentication, Polly resilience, and `BaseAddress` as every read.

> [!WARNING]
> A field marked `[ODataField(IgnoreOnUpdate = true)]` is dropped from the PATCH payload for a reason: if it reaches D365, the server rejects the **whole** PATCH with HTTP 403 `ODataSecurityException` ("update not allowed for field 'X'"), not only that field. On `LedgerJournalHeader` this covers `JournalName`, `AccountingCurrency`, `IsPosted`, `JournalTotalDebit`, and `JournalTotalCredit`. Never re-add a read-only field to the update path.

> [!NOTE]
> A composite-key PATCH returns `204 No Content` — D365 does not echo the entity. `UpdateAsync` returns the caller's entity in that case, so a successful `Result<LedgerJournalHeader>` always carries a non-null `Value`. Verified against live D365 (JFI) on 2026-07-01.

## Delete a record

```csharp
Result<LedgerJournalHeader> result = await mediator
    .Send(new DeleteCommand<LedgerJournalHeader>(header), cancellationToken)
    .ConfigureAwait(false);
```

`DeleteCommand<TEntity>` needs only the composite key populated on `header`. It issues the DELETE through the same composite-key bypass as Update.

## Handle the failure path

Every single command returns `Result<T>`. Branch on `result.IsFailed`, read `result.GetError()`, and map `IntegrationError.Type` to a response.

```csharp
Result<LedgerJournalHeader> result = await mediator
    .Send(new UpdateCommand<LedgerJournalHeader>(header), cancellationToken)
    .ConfigureAwait(false);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    // A read-only field in the payload surfaces here as LedgerJournalHeader.Unauthorized (HTTP 403,
    // ErrorType.Failure). The message is generic; the D365 detail is in the host log and error.Exception.
    return error?.Type switch
    {
        ErrorType.Validation => HttpStatusCode.BadRequest,
        ErrorType.NotFound   => HttpStatusCode.NotFound,
        ErrorType.Conflict   => HttpStatusCode.Conflict,
        _                    => HttpStatusCode.InternalServerError,
    };
}
```

`ErrorType` has exactly four members: `Failure`, `Validation`, `NotFound`, `Conflict`. A malformed composite key (for example a null `JournalBatchNumber`) fails before the HTTP call with `ErrorType.Validation`. See [Handle Errors](Handle-Errors) for the full model and `Match` matching.

## Send a batch

Batch commands apply one operation across a collection. They take `IReadOnlyList<TEntity>` plus optional `BatchOptions`, and return `Result<BatchOutcome>` — a per-item report of what committed and what failed (since v3.0.0). Input larger than `ODataSettings.Batch.MaxOperationsPerChunk` (default 150) is split into chunks automatically; D365 caps a `$batch` at 200 operations.

```csharp
using IntegratoR.Abstractions.Common.Batch;

DateTimeOffset transDate = new(DateTime.UtcNow.Date, TimeSpan.Zero);

// AccountDisplayValue and TransDate are compiler-required but [ODataField(IgnoreOnCreate = true)],
// so D365 does not receive them on create — see Known Limitations.
IReadOnlyList<LedgerJournalLine> lines =
[
    new()
    {
        DataAreaId = "USMF", JournalBatchNumber = "B0001", AccountDisplayValue = "110180-",
        TransDate = transDate, DebitAmount = 1000m, CreditAmount = 0m, CurrencyCode = "USD",
    },
    new()
    {
        DataAreaId = "USMF", JournalBatchNumber = "B0001", AccountDisplayValue = "211100-",
        TransDate = transDate, DebitAmount = 0m, CreditAmount = 1000m, CurrencyCode = "USD",
    },
];

Result<BatchOutcome> result = await mediator
    .Send(new CreateBatchCommand<LedgerJournalLine>(lines), cancellationToken)
    .ConfigureAwait(false);

if (result.IsSuccess)
{
    BatchOutcome outcome = result.Value;   // outcome.AllSucceeded is true
}
```

See [Known Limitations](Known-Limitations#ledgerjournalline-attribute-audit-incomplete) for the `LedgerJournalLine` create-payload gap.

### Choose a failure mode

`BatchFailureMode` decides how each chunk goes over the wire. No batch write uses PanoramicData; every request goes through the named `"ODataClient"` with the same authentication, Polly resilience, and `BaseAddress` as a single write.

| Mode | Sent as | Atomicity |
|---|---|---|
| `Atomic` (default) | One multipart `$batch` POST per chunk, carrying a single changeset | All-or-nothing **per chunk** |
| `ContinueOnError` | One POST / PATCH / DELETE per item, in sequence | None — each item commits or fails on its own |

Override per call with `BatchOptions`, or set the defaults under `ODataSettings.Batch` (see [Configure OData](Configure-OData#batch-options)):

```csharp
Result<BatchOutcome> result = await mediator.Send(
    new UpdateBatchCommand<LedgerJournalHeader>(
        headers,
        new BatchOptions { Mode = BatchFailureMode.ContinueOnError, MaxOperationsPerChunk = 100 }),
    cancellationToken);
```

A per-call `MaxOperationsPerChunk` is clamped to 1–200; the configured default is validated to the same range at startup.

> [!CAUTION]
> `Atomic` is all-or-nothing **per chunk**, not per dataset — a collection larger than the chunk size becomes several independent changesets. `StopOnFirstFailedChunk` (default `true`, `Atomic` only) halts after the first failed chunk, but chunks already committed stay committed. The batch write path is covered by unit and integration tests; it has not yet been verified against a live D365 environment. See [Known Limitations](Known-Limitations#batch-atomicity-is-per-chunk).

### Read the failure list

A batch that ran and lost at least one operation is a failed result whose first error is a `BatchIntegrationError` carrying the full outcome, so you can pinpoint which records to retry:

```csharp
if (result.IsFailed)
{
    IntegrationError? error = result.GetError();

    if (error is BatchIntegrationError batchError)
    {
        foreach (BatchItemResult failure in batchError.Outcome.Failures)
        {
            // failure.Index, failure.ChunkIndex, failure.Key, failure.StatusCode, failure.ErrorMessage
        }
    }
    else
    {
        // Rejected before anything was sent: Validation.Error, {Entity}.InvalidKey, {Entity}.KeyCountMismatch.
    }
}
```

Not every failure carries an outcome:

- **Null or empty list** — `ValidationBehaviour` returns `Validation.Error` before the handler runs.
- **Bad key on update or delete** — a null key element or one containing a control character returns `{Entity}.InvalidKey`; a key whose value count does not match the `[Key]` properties returns `{Entity}.KeyCountMismatch`. Both are `ErrorType.Validation`, and every entity is checked before the first chunk is sent (control-character rejection since v3.0.0).
- **Missing required field on create** — a null `[ODataField(IsRequired = true)]` field returns `{Entity}.RequiredFieldMissing` (`ErrorType.Validation`) before anything is sent, as a single create does. Required fields are not checked on update.

Inside an outcome, two status codes need reading:

- **`StatusCode` 0** — the chunk could not be sent or its response was lost (a socket failure that outlasted the retries, an open circuit breaker, or an `HttpClient` timeout). Every item in that chunk is marked failed with the exception message, and whether D365 applied it is unknown.
- **`StatusCode` 502** — an atomic changeset did not commit and its sub-responses could not be matched to the operations sent (since v3.0.0).

Only your own cancellation propagates as an exception. See [Handle Errors](Handle-Errors#what-still-throws).

| Command | HTTP method | Use |
|---|---|---|
| `CreateBatchCommand<TEntity>` | POST | Bulk insert |
| `UpdateBatchCommand<TEntity>` | PATCH | Bulk update |
| `DeleteBatchCommand<TEntity>` | DELETE | Bulk delete |

## Send an entity-specific command

`IntegratoR.OData.FO` ships entity-specific command records that inherit the generic commands and route to handlers adding ledger-specific logging context. Reach for one only when that context is worth the extra type.

```csharp
// Generic — the default for most callers.
await mediator.Send(new CreateCommand<LedgerJournalHeader>(header), cancellationToken)
    .ConfigureAwait(false);

// Entity-specific — same result, plus ledger logging context.
await mediator.Send(new CreateLedgerJournalHeaderCommand<LedgerJournalHeader>(header), cancellationToken)
    .ConfigureAwait(false);
```

The shipped records include `CreateLedgerJournalHeaderCommand<T>`, `UpdateLedgerJournalHeaderCommand<T>`, `CreateLedgerJournalLineCommand<T>`, `UpdateLedgerJournalLineCommand<T>`, and plural batch variants (`…HeadersCommand<T>`, `…LinesCommand<T>`). Each constrains `T` to the matching entity — for example `CreateLedgerJournalHeaderCommand<TEntity> where TEntity : LedgerJournalHeader`. The plural records take only the list, so they run with the `ODataSettings.Batch` defaults unless you set `Options` in an object initialiser.

**Prefer the generic command.** Choose an entity-specific command when its ledger logging context is genuinely useful in your telemetry; choose the generic `CreateCommand<T>` otherwise.

## Write your own command

For entity-specific defaults, chained operations, or a bound D365 action, define a `record` implementing `ICommand<TResponse>` and pair it with an `IRequestHandler`.

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Commands;

public record PostJournalCommand(string DataAreaId, string JournalBatchNumber) : ICommand<Result>
{
    // ICommand<T> carries IContext; LoggingBehaviour reads this for structured logs.
    public IReadOnlyDictionary<string, object> GetLoggingContext() =>
        new Dictionary<string, object>
        {
            ["DataAreaId"] = DataAreaId,
            ["JournalBatchNumber"] = JournalBatchNumber,
        };
}
```

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;

public sealed class PostJournalCommandHandler(IService<LedgerJournalHeader> service)
    : IRequestHandler<PostJournalCommand, Result>
{
    public async Task<Result> Handle(PostJournalCommand request, CancellationToken cancellationToken)
    {
        // Invoke a D365 bound action, chain a status update, and translate any
        // downstream failure into Result.Fail(new IntegrationError(...)).
        return Result.Ok();
    }
}
```

Register the handler's assembly through the `AddConsumerHandlers(...)` step inside `AddIntegratoR`; the pipeline then resolves it like any built-in command. For a command that reports only success or failure, implement the non-generic `ICommand` (its handler returns `Task<Result>`).

## Where commands run in the pipeline

Every command flows through the MediatR pipeline — Logging, then Validation, then Caching, then the handler — before it reaches `ODataService<TEntity>`. Polly retry and the circuit breaker sit below that, at the `HttpClient` layer, and are transparent to the command — including for writes, which are retried today (see [Known Limitations](Known-Limitations#writes-are-retried)). See [Extend the Pipeline](Extend-the-Pipeline) for the behaviour chain and [Configure Resilience](Configure-Resilience) for the retry and circuit-breaker settings.

## See Also

- [Define Entities](Define-Entities) — `[ODataField]`, `[JsonPropertyName]`, and composite keys
- [Handle Errors](Handle-Errors) — process the `Result<T>` every command returns
- [Add Validation](Add-Validation) — pre-flight checks in the pipeline
- [Extend the Pipeline](Extend-the-Pipeline) — behaviour order and custom behaviours
