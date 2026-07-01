# Send Commands
> Last verified against v2.0.1

Commands modify D365 F&O state through OData create (POST), update (PATCH), and delete (DELETE) requests. Send them with `IMediator.Send`; each returns a `Result<T>` (single) or `Result` (batch) — no command throws for a business failure.

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
    // A read-only field in the payload surfaces here as a Failure with the D365 403 message.
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

Batch commands apply one operation across a collection. They take `IReadOnlyList<TEntity>` (since v2.0.0) and return non-generic `Result` — no per-entity values on success.

```csharp
IReadOnlyList<LedgerJournalLine> lines =
[
    new()
    {
        DataAreaId = "USMF",
        JournalBatchNumber = "B0001",
        DebitAmount = 1000m,
        CreditAmount = 0m,
        CurrencyCode = "USD",
    },
    new()
    {
        DataAreaId = "USMF",
        JournalBatchNumber = "B0001",
        DebitAmount = 0m,
        CreditAmount = 1000m,
        CurrencyCode = "USD",
    },
];

Result result = await mediator
    .Send(new CreateBatchCommand<LedgerJournalLine>(lines), cancellationToken)
    .ConfigureAwait(false);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    // Handle the first failing item; see the atomicity note below.
}
```

| Command | Effective HTTP method | Use |
|---|---|---|
| `CreateBatchCommand<TEntity>` | POST per entity | Bulk insert |
| `UpdateBatchCommand<TEntity>` | PATCH per entity | Bulk update |
| `DeleteBatchCommand<TEntity>` | DELETE per entity | Bulk delete |

> [!CAUTION]
> Batch **Update** and **Delete** over composite keys run **per item, not atomically** — each entity is its own PATCH/DELETE through the raw-`HttpClient` bypass. If item three fails, items one and two are already committed and are not rolled back. Only the PanoramicData create changeset is a single OData transaction. Size batches so a partial failure is recoverable, and key retries on the composite key rather than replaying the whole list.

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

The shipped records include `CreateLedgerJournalHeaderCommand<T>`, `UpdateLedgerJournalHeaderCommand<T>`, `CreateLedgerJournalLineCommand<T>`, `UpdateLedgerJournalLineCommand<T>`, and plural batch variants (`…HeadersCommand<T>`, `…LinesCommand<T>`). Each constrains `T` to the matching entity — for example `CreateLedgerJournalHeaderCommand<TEntity> where TEntity : LedgerJournalHeader`.

**Prefer the generic command.** Choose an entity-specific command when its ledger logging context is genuinely useful in your telemetry; choose the generic `CreateCommand<T>` otherwise.

## Write your own command

For entity-specific defaults, chained operations, or a bound D365 action, define a `record` implementing `ICommand<TResponse>` and pair it with an `IRequestHandler`.

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Commands;

public record PostJournalCommand(string DataAreaId, string JournalBatchNumber)
    : ICommand<Result>;
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

Every command flows through the MediatR pipeline — Logging, then Validation, then Caching, then the handler — before it reaches `ODataService<TEntity>`. Polly retry and the circuit breaker sit below that, at the `HttpClient` layer, and are transparent to the command. See [Extend the Pipeline](Extend-the-Pipeline) for the behaviour chain and [Configure Resilience](Configure-Resilience) for the retry and circuit-breaker settings.

## See Also

- [Define Entities](Define-Entities) — `[ODataField]`, `[JsonPropertyName]`, and composite keys
- [Handle Errors](Handle-Errors) — process the `Result<T>` every command returns
- [Add Validation](Add-Validation) — pre-flight checks in the pipeline
- [Extend the Pipeline](Extend-the-Pipeline) — behaviour order and custom behaviours
