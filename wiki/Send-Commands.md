# Send Commands

Commands modify D365 F&O state via OData create (POST), update (PATCH), and delete (DELETE) requests. The framework provides generic commands that work with any entity implementing `IEntity` — custom command types are only required when entity-specific logic (logging context, defaults, chained operations) is needed.

## Create a Record

```csharp
LedgerJournalHeader header = new()
{
    DataAreaId = "USMF",
    JournalName = "GenJrn",
    Description = "Monthly accruals"
};

Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(header),
    cancellationToken).ConfigureAwait(false);

if (result.IsSuccess)
{
    string batchNumber = result.Value.JournalBatchNumber!;  // server-assigned
}
```

`CreateCommand<TEntity>` is a `record` with a positional constructor taking the entity to create. The handler:

1. Reads `[ODataField(IgnoreOnCreate)]` and `AllowEditOnCreate = false` annotations and strips matching properties from the payload.
2. Serialises the remaining properties using the entity's `[JsonPropertyName]` mappings.
3. POSTs to `<ODataSettings.Url>/<TableName>` where `TableName` comes from `[Table(...)]`.
4. Deserialises the response into a fresh `TEntity` (with server-generated fields populated) and wraps it in `Result.Ok(...)`.

## Update a Record

```csharp
header.Description = "Updated description";

Result<LedgerJournalHeader> result = await mediator.Send(
    new UpdateCommand<LedgerJournalHeader>(header),
    cancellationToken).ConfigureAwait(false);
```

`UpdateCommand<TEntity>` sends a PATCH to the composite-key URL. Fields marked `IgnoreOnUpdate` or with `AllowEdit = false` are excluded from the payload. The composite key on the entity must be fully populated — the framework reads `GetCompositeKey()` to build the key segment.

> The composite-key URL for the update path has a known limitation in the current PanoramicData.OData.Client release — see [Known Limitations](Known-Limitations#composite-key-writes) for the parked workaround design. Reads via `GetByKeyQuery<T>` use a different code path that already handles composite keys via the `Filter()` bypass.

## Delete a Record

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(
    new DeleteCommand<LedgerJournalHeader>(header),
    cancellationToken).ConfigureAwait(false);
```

The composite key on the entity must be populated. The same composite-key write limitation noted above applies.

## Batch Operations

Batch commands send the same operation against a collection of entities. Each entity is processed individually — if one fails, the result captures the error but earlier successes are not rolled back (D365's OData surface does not expose a transactional batch endpoint at the entity-set level).

```csharp
List<LedgerJournalLine> lines =
[
    new()
    {
        DataAreaId = "USMF",
        JournalBatchNumber = "JBN-000431",
        DebitAmount = 1000m,
        CreditAmount = 0m,
        CurrencyCode = "USD"
    },
    new()
    {
        DataAreaId = "USMF",
        JournalBatchNumber = "JBN-000431",
        DebitAmount = 0m,
        CreditAmount = 1000m,
        CurrencyCode = "USD"
    }
];

Result result = await mediator.Send(
    new CreateBatchCommand<LedgerJournalLine>(lines),
    cancellationToken).ConfigureAwait(false);
```

Batch commands return non-generic `Result` (no per-entity values on success). The three batch shapes:

| Command | Effective HTTP method | Use case |
|---|---|---|
| `CreateBatchCommand<TEntity>` | POST per entity | Bulk insert |
| `UpdateBatchCommand<TEntity>` | PATCH per entity | Bulk update |
| `DeleteBatchCommand<TEntity>` | DELETE per entity | Bulk delete |

For high-volume work the consumer can parallelise by splitting the collection and sending multiple batches concurrently. The OData client respects the Polly circuit breaker — concurrent batches against a degraded D365 environment fail fast rather than amplifying load.

## Pipeline Flow for Commands

Every command passes through the MediatR pipeline in the registration order:

1. **`LoggingBehaviour`** — logs request type, duration, and `IContext`-derived structured properties (entity type, composite key, etc.).
2. **`ValidationBehaviour`** — runs registered FluentValidation validators; returns `Result.Fail(IntegrationError(... type: Validation))` and short-circuits the pipeline on the first failure.
3. **`CachingBehaviour`** — short-circuits to cache hit when the request implements `ICacheableQuery<T>`. Commands are never `ICacheableQuery`, so this is a pass-through.
4. **Handler** — the closed-generic `CreateCommandHandler<TEntity>` / `UpdateCommandHandler<TEntity>` / `DeleteCommandHandler<TEntity>` calls `ODataService<TEntity>` to execute the HTTP operation.

The Polly retry and circuit breaker live below this pipeline at the `HttpClient` layer — wrapped in the `DelegatingHandler` chain inside `AddODataClient`. Retries are transparent to the MediatR pipeline.

## Custom Commands

When entity-specific logic is needed (custom logging context, default values, pre-validation, chained operations), define a `record` that implements `ICommand<TResponse>`:

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Commands;

public record PostJournalCommand(string DataAreaId, string JournalBatchNumber)
    : ICommand<Result>;
```

Pair it with a handler that implements `IRequestHandler<PostJournalCommand, Result>`:

```csharp
public sealed class PostJournalCommandHandler
    : IRequestHandler<PostJournalCommand, Result>
{
    private readonly IService<LedgerJournalHeader> _service;
    private readonly ILogger<PostJournalCommandHandler> _logger;

    public PostJournalCommandHandler(
        IService<LedgerJournalHeader> service,
        ILogger<PostJournalCommandHandler> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task<Result> Handle(PostJournalCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Posting journal {DataAreaId}/{BatchNumber}",
            request.DataAreaId, request.JournalBatchNumber);

        // Custom logic: invoke a D365 OData bound action, chain a status update, etc.
        return Result.Ok();
    }
}
```

Register the handler's assembly through `AddConsumerHandlers(...)` and the pipeline picks it up automatically.

For a non-state-returning command (e.g. fire-and-forget) use the non-generic `ICommand` interface:

```csharp
public record ArchiveJournalCommand(string DataAreaId, string JournalBatchNumber)
    : ICommand;
// handler returns Task<Result>, no payload
```

### Entity-Specific Built-Ins

`IntegratoR.OData.FO` ships entity-specific command types as examples — they inherit from the generic commands and route to dedicated handlers that add ledger-specific logging context:

- `CreateLedgerJournalHeaderCommand<T>` / `CreateLedgerJournalHeadersCommand<T>` (plural variant for batch)
- `CreateLedgerJournalLineCommand<T>` / `CreateLedgerJournalLinesCommand<T>`
- `UpdateLedgerJournalHeaderCommand<T>` / `UpdateLedgerJournalHeadersCommand<T>`
- `UpdateLedgerJournalLineCommand<T>` / `UpdateLedgerJournalLinesCommand<T>`

For most consumers the generic `CreateCommand<LedgerJournalHeader>` is enough — pick the entity-specific variant only when the entity-specific log context is genuinely useful.

## Error Handling

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(command, cancellationToken)
    .ConfigureAwait(false);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    return error?.Type switch
    {
        ErrorType.Validation => HttpStatusCode.BadRequest,
        ErrorType.NotFound   => HttpStatusCode.NotFound,
        ErrorType.Conflict   => HttpStatusCode.Conflict,
        _                    => HttpStatusCode.InternalServerError
    };
}
```

See [Handle Errors](Handle-Errors) for the full error model, `Match` pattern matching, and how to surface errors back to HTTP callers.

## See Also

- [Define Entities](Define-Entities) — `[ODataField]`, `[JsonPropertyName]`, composite keys
- [Run Queries](Run-Queries) — fetch records before updating or deleting
- [Handle Errors](Handle-Errors) — process the `Result<T>` returned by every command
- [Add Validation](Add-Validation) — pre-flight checks via FluentValidation in the pipeline
- [Extend the Pipeline](Extend-the-Pipeline) — custom MediatR behaviours
- [Known Limitations](Known-Limitations) — composite-key Update/Delete workaround status
