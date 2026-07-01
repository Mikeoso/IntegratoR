# Understand the Architecture
> Last verified against v2.0.1

IntegratoR is Clean Architecture with dependencies pointing inward: the concrete infrastructure layers (`OData`, `OData.FO`, `Application`, `Hosting`) reference the abstract core (`Abstractions`), never the reverse. A consumer wires the whole graph with one call — `AddIntegratoR(configuration)` — and drives it through `IMediator`. The rest of this page states the durable invariants that outlive any single file.

## Dependency direction points inward

Every arrow points at `IntegratoR.Abstractions`. Higher layers depend on the core; the core depends on nothing above it. This is what lets a consumer reference only `IService<T>` and `Result<T>` from `Abstractions` for testability while the concrete `ODataService<T>` lives in `IntegratoR.OData` and is bound at the composition root.

```
SampleFunction (host / composition root)
  -> Hosting     -> Application -> Abstractions
                 -> OData       -> Abstractions
                 -> OData.FO    -> OData -> Abstractions
```

| Project | Role | Depends on |
|---|---|---|
| `IntegratoR.Abstractions` | Domain interfaces (`IService<T>`, `ICommand<T>`, `IQuery<T>`), the non-generic `BaseEntity`, CQRS contracts, `Result<T>` types (`IntegrationError`, `ErrorType`), and both `Result<T>` JSON converter families | — (core) |
| `IntegratoR.Application` | MediatR pipeline behaviours, generic command/query handlers, `OAuthAuthenticator`, cache services | Abstractions |
| `IntegratoR.OData` | Generic OData client, `ODataService<T>`, auth handler, Polly policies, `ODataFieldAttribute`, the LINQ→`$filter` translator | Abstractions |
| `IntegratoR.OData.FO` | D365 F&O entities (`LedgerJournalHeader`/`Line`), dimension queries, feature handlers, `FOSettings` | OData |
| `IntegratoR.Hosting` | `IntegratoRBuilder` and the single `AddIntegratoR` composition root | Application, OData, OData.FO |
| `IntegratoR.SampleFunction` | Isolated-worker Functions host — the consumer-side composition root and smoke triggers | Hosting (not published) |
| `IntegratoR.TestKit` | Shared test infrastructure: `Result<T>` assertions, fakes, entity builders | test-only |

> [!NOTE]
> `AddApplicationServices`, `AddODataClient`, and `AddODataClientFOProxy` are internal composition steps, not consumer API. Call `AddIntegratoR` — it is the only public entry point, and it invokes the three in order (see below).

## Entities inherit the non-generic BaseEntity

An entity inherits `BaseEntity` and overrides `object[] GetCompositeKey()`, returning key-field values in a fixed order. D365 keys are composite — typically `DataAreaId` plus a business key.

```csharp
[Table("LedgerJournalHeaders")]
public class LedgerJournalHeader : BaseEntity
{
    [JsonPropertyName("dataAreaId")]
    public required string DataAreaId { get; set; }

    [ODataField(IgnoreOnCreate = true)]      // server-assigned
    public string? JournalBatchNumber { get; set; }

    [ODataField(IgnoreOnUpdate = true)]      // read-only on update in D365
    public required string JournalName { get; set; }

    public required string Description { get; set; }

    public override object[] GetCompositeKey() => [DataAreaId, JournalBatchNumber!];
}
```

The generic `BaseEntity<TKey>` is `[Obsolete]` (the type parameter was never used) and is removed next MAJOR. Never use it. See [Define Entities](Define-Entities) for the full `[ODataField]`/`[JsonPropertyName]` matrix.

## CQRS runs through MediatR

Commands and queries are `record` types implementing `ICommand<TResponse>` or `IQuery<TResponse>`. A consumer never calls `ODataService<T>` directly — it sends a command or query and reads back a `Result<T>`.

```csharp
LedgerJournalHeader header = new()
{
    DataAreaId = "USMF",
    JournalName = "GenJrn",
    Description = "April accruals",
};

Result<LedgerJournalHeader> result =
    await mediator.Send(new CreateCommand<LedgerJournalHeader>(header), cancellationToken);

if (result.IsFailed)
{
    IntegrationError error = result.GetError();
    // Code is "{EntityType}.{reason}" — e.g. "LedgerJournalHeader.Conflict" on an HTTP 409 (ErrorType.Conflict).
    return;
}

string batchNumber = result.Value.JournalBatchNumber!;  // server-assigned on create
```

Generic `CreateCommand<T>`/`UpdateCommand<T>`/`DeleteCommand<T>` and `GetByKeyQuery<T>`/`GetByFilterQuery<T>` work with any `IEntity`; entity-specific commands compose them only when they add real logging context. See [Send Commands](Send-Commands) and [Run Queries](Run-Queries).

## Result&lt;T&gt; never throws for business flow

Every operation returns `Result<T>` (or non-generic `Result`) from FluentResults. Business failures return a failed `Result` carrying an `IntegrationError { string Code; ErrorType Type; Exception? Exception }`; they do not throw. `ErrorType` has four members: `Failure`, `Validation`, `NotFound`, `Conflict`. Exceptions are reserved for genuinely exceptional infrastructure faults (cancellation, socket resets).

Read failures with `result.IsFailed` and `result.GetError()` — never `try`/`catch` on the pipeline, never the verbose `result.Errors.FirstOrDefault()` form. See [Handle Errors](Handle-Errors).

## The pipeline order is fixed: Logging → Validation → Caching → Handler

`AddApplicationServices` registers three MediatR behaviours in one order, and the order is load-bearing.

1. **`LoggingBehaviour`** — records request type, duration, and `IContext.GetLoggingContext()`, and detects a failed `Result<T>` returned by an inner stage.
2. **`ValidationBehaviour`** — runs every `IValidator<TRequest>` and short-circuits on the first failure with `IntegrationError("Validation.Error", …, Validation)`.
3. **`CachingBehaviour`** — acts only on `ICacheableQuery<TResponse>`; a hit short-circuits, a miss runs the handler and caches a successful response.
4. **Handler** — the closed generic `CreateCommandHandler<T>` / `GetByKeyQueryHandler<T>` or a consumer handler.

> [!CAUTION]
> The order is deliberate. Validation before caching stops an invalid request from ever producing a cache entry; caching before the handler stops a valid cache hit from re-running work. Custom behaviours registered via `AddTransient(typeof(IPipelineBehavior<,>), …)` run **after** these three, between caching and the handler. See [Extend the Pipeline](Extend-the-Pipeline).

Because `LoggingBehaviour` inspects the returned `Result<T>` rather than only catching exceptions, a handler that returns `Result.Fail(...)` is logged as a failure — a failed `Result` is never silently logged as success.

## Two JSON serialisers, kept in lockstep

The codebase runs both System.Text.Json and Newtonsoft.Json, and `Result<T>` needs converters in both. Both families delegate to one shared shape helper, `ResultJsonShape`, so the wire format never drifts.

| Serialiser | Used by | Wiring |
|---|---|---|
| System.Text.Json | Durable Functions data converter, `DistributedCacheService` | **Auto** — `AddIntegratoR` wires `DurableTaskWorkerOptions.DataConverter`; the cache service registers converters in its own static options. Consumers do nothing. |
| Newtonsoft.Json | HTTP trigger payloads, journal-file parsing | **Manual** — a `JsonConvert.DefaultSettings` block in the host `Program.cs`. |

> [!NOTE]
> `ResultJsonShape.Project` accepts any `IError`, falling back to `(Unknown, message, Failure)` for a plain `Result.Fail("message")`. That leniency is intentional public-API behaviour — external consumers may pass non-`IntegrationError` results. See [Set Up Azure Functions Host](Set-Up-Azure-Functions-Host) for the Newtonsoft snippet.

## Composite-key writes bypass PanoramicData

D365 F&O entities are composite-keyed, and PanoramicData's `Key(object)` cannot bind a dictionary key — it calls `.ToString()` on the dictionary and emits a malformed URL. So Update, Delete, and their batch variants route through an owned raw-`HttpClient` bypass in `ODataClientAdapter`. It builds the keyed URL by hand — `LedgerJournalHeaders(dataAreaId='USMF',JournalBatchNumber='B0001')` — using the `[JsonPropertyName]` wire names, and issues the request through the named `"ODataClient"` client so the write carries the same authentication, Polly resilience, and `BaseAddress` as every other call (since v2.0.0). Reads use a parallel `$filter` bypass over the same key fields.

> [!NOTE]
> D365 returns `204 No Content` on a composite-key PATCH. `UpdateAsync` treats that as success and returns the caller's entity, so a successful `Result<TEntity>` never carries a null `Value`.

> [!WARNING]
> A single field marked `[ODataField(IgnoreOnUpdate = true)]` present in an update payload makes D365 reject the **whole** PATCH with HTTP 403 (`ODataSecurityException`), not only that field. On `LedgerJournalHeader` that set is `JournalName`, `AccountingCurrency`, `IsPosted`, `JournalTotalDebit`, and `JournalTotalCredit`. Audit every field against D365's update semantics before shipping an entity.

## The composition root wires it in order

`AddIntegratoR(configuration, configure)` runs these steps, and `ODataSettingsValidator : IValidateOptions<ODataSettings>` (auto-registered, with `ValidateOnStart`) fails the host at start-up on invalid connection or auth settings rather than at first request.

1. `AddApplicationServices()` — pipeline behaviours, MediatR, cache, OAuth authenticator.
2. `AddODataClient(configuration)` — HTTP client, Polly policies, `ODataService<T>`, `ODataSettingsValidator`.
3. `AddODataClientFOProxy(configuration)` — F&O handlers and entity bindings.
4. A combined MediatR scan (Application + F&O + every assembly from `AddConsumerHandlers`) that closes the open-generic CRUD handlers against F&O **and** consumer entity types together.
5. The Durable Functions `Result<T>` converter on `DurableTaskWorkerOptions` (lazy — zero cost for non-Durable consumers).
6. Closing and registering the open-generic and consumer validators so `ValidationBehaviour` resolves them.

Step 4 exists because MediatR v12 closes open generics only within a single scanned assembly, and step 6 exists because FluentValidation's scanner skips open generics. Both cross-assembly closures happen once, here — a consumer's extended entities get closed generic handlers and validators exactly like the framework's own. The rationale lives in [ADR-0001](https://github.com/Mikeoso/IntegratoR/blob/main/docs/adr/0001-generic-handler-and-validator-registration.md); the Durable wiring in [ADR-0002](https://github.com/Mikeoso/IntegratoR/blob/main/docs/adr/0002-durable-functions-result-converter-wiring.md); the `[JsonPropertyName]`-aware translator fork in [ADR-0003](https://github.com/Mikeoso/IntegratoR/blob/main/docs/adr/0003-odata-expression-translator-fork.md).

## See Also

- [Getting Started](Getting-Started)
- [Send Commands](Send-Commands)
- [Extend the Pipeline](Extend-the-Pipeline)
- [Set Up Azure Functions Host](Set-Up-Azure-Functions-Host)
