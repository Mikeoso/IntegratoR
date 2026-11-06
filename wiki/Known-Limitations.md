# Known Limitations
> Last verified against v3.0.0

Every limitation here is real today, has a documented cause, and states its workaround. Resolved gaps move to [Release Notes and Versioning](Release-Notes-and-Versioning) — they do not linger on this list.

## No cross-company queries

D365 OData accepts a `cross-company=true` parameter that returns rows across every legal entity the service principal can read. IntegratoR does not surface it — every query is scoped to the company you set through the entity composite key.

Read one company at a time and merge the results:

```csharp
var companies = new[] { "USMF", "JFI" };
var journals = new List<LedgerJournalHeader>();

foreach (string company in companies)
{
    var query = new GetByFilterQuery<LedgerJournalHeader>(
        header => header.DataAreaId == company && header.IsPosted == NoYes.No);
    Result<IEnumerable<LedgerJournalHeader>> result = await mediator.Send(query, cancellationToken);

    if (result.IsFailed)
    {
        IntegrationError? error = result.GetError();
        // IntegrationError { Code, Type }: a D365 read failure surfaces here — inspect and stop or skip.
        break;
    }

    journals.AddRange(result.Value);
}
```

**Workaround status:** not planned. The likely shape is a per-query opt-in marker (`ICrossCompanyQuery`) handled by a dedicated pipeline behaviour; the design is not worked out.

## Narrow `$expand` translator coverage

The LINQ-to-OData translator (`IntegratoRODataExpressionTranslator`) covers filter, select, and expand expressions plus `Any` / `All` lambdas. Advanced `$expand` shapes — nested expand with a filter, expand carrying a select inside an `Any` — are not covered and throw `NotSupportedException` at translation time.

The failure is caught before the request reaches D365, so an unsupported expression never produces a malformed call. Through a query, `ODataService` returns it as a failed result, `{Entity}.UnexpectedError`, with the `NotSupportedException` in `error.Exception`:

```csharp
// Supported: a flat expand translates cleanly.
var supported = new GetByFilterQuery<LedgerJournalHeader>(
    header => header.DataAreaId == "USMF");

// Unsupported: a nested expand-with-filter throws NotSupportedException at translation,
// not a runtime OData error. The expression never leaves the process.
```

The supported shapes are pinned by `tests/IntegratoR.OData.Tests/Common/Filters/IntegratoRODataExpressionTranslatorTests.cs`.

**Workaround status:** coverage is added on demand. The translator stays narrow on purpose — predictable, D365-compatible output over exhaustive LINQ coverage.

## `LedgerJournalLine` attribute audit incomplete

`LedgerJournalLine` carries fields that are both `required` in C# and `[ODataField(IgnoreOnCreate = true)]` — the value is compiler-mandatory but excluded from the create payload. `AccountDisplayValue` and `TransDate` are the confirmed pair today; the wider set of `IgnoreOnCreate` fields has not been audited against D365's create metadata.

```csharp
// LedgerJournalLine.cs — required by the compiler, yet dropped from the create body:
[JsonPropertyName("AccountDisplayValue")]
[ODataField(IgnoreOnCreate = true)]
public virtual required string AccountDisplayValue { get; set; }
```

> [!CAUTION]
> A field marked both `required` and `IgnoreOnCreate` is silent payload exclusion — D365 either computes a default and accepts the create, or rejects it with HTTP 400 and a server-side validation message, depending on the journal template. Read the entity source before building a `LedgerJournalLine` create so you know which values D365 actually receives.

**Workaround status:** queued. Each field needs verifying against D365 metadata — server-generated (keep `IgnoreOnCreate`) or consumer-supplied (drop it) — and lands with a reflection-based test pinning the attribute state.

## Writes are retried

The named `"ODataClient"` retry policy has no HTTP-method guard. It retries POST, PATCH, DELETE, and every `$batch` chunk POST exactly like a read — on a 5xx, 408, or 429 response, a network failure, or a `TaskCanceledException`. Single-entity writes are retried again by the operation-level policy inside `ODataService` (a transient `ODataClientException` or a `TaskCanceledException`), so the two layers compound.

A retry is safe only when the write is idempotent. A create that D365 committed but answered with a 5xx — or too late for the client — is sent again, and the repeat creates a duplicate: a second journal header, or a second set of lines from a retried `Atomic` chunk. A repeated update re-applies the same values; a repeated batch delete reports HTTP 404 failures for rows that are already gone.

```csharp
// Turns off both retry layers. Reads lose their retries too.
services.AddIntegratoR(configuration, integrator =>
    integrator.ConfigureOData(settings => settings.Resilience.EnableRetries = false));
```

**Workaround status:** a fix that limits retries to idempotent requests is planned after v3.0.0. Until then, either disable retries as above or make creates detectable — set a value you can filter on (for example `Description` or `IntegrationKey` on `LedgerJournalHeader`) and query for it before re-running a failed import.

## Polly may retry HTTP 400 from APIM

The HTTP retry policy handles transient errors plus `TaskCanceledException` (`.HandleTransientHttpError().Or<TaskCanceledException>()`). APIM surfaces some gateway timeouts as HTTP 400 rather than 408 or 504, so a chronic 400 can be retried up to `RetryCount` times before the request fails.

The retries are visible, not silent — each one logs a warning to the `IntegratoR.OData.HttpRetry` logger with a rising attempt count:

```text
warn: IntegratoR.OData.HttpRetry
      HTTP retry attempt 3 after 4000ms. Reason: BadRequest
```

**Workaround status:** uninvestigated. The likely fix tightens the retry predicate — drop `Or<TaskCanceledException>` for HTTP retries (an `HttpClient.Timeout` cancellation is already 408-equivalent) or add an explicit status-code filter that excludes 400.

## `RetryCount` range 1–10 is documented, not enforced

`ODataResilienceSettings.RetryCount` defaults to `3` and its XML doc states a valid range of 1 to 10. `ODataSettingsValidator` does not check that range — it validates authentication headers, the selected mode and its credentials, and the batch chunk-size bound (`Batch.MaxOperationsPerChunk`, 1–200). A `RetryCount` of `0` or `50` binds and runs.

> [!CAUTION]
> Keep `RetryCount` within 1–10 yourself. A value of `0` disables retries for that policy; a large value multiplies load against D365 and delays the surfaced failure. Nothing rejects an out-of-range value at startup.

**Workaround status:** the range check is a candidate addition to `ODataSettingsValidator`; not yet implemented.

## Batch atomicity is per-chunk

`Atomic` batch mode runs each chunk as one all-or-nothing OData changeset, but a dataset larger than the chunk size (`ODataSettings.Batch.MaxOperationsPerChunk`, default 150; D365 caps a `$batch` at 200 operations) is split across several changesets. Each chunk commits or rolls back independently — a 1,000-row import is not a single transaction.

```csharp
// StopOnFirstFailedChunk (default true in Atomic mode) stops submitting further chunks after one
// fails, but chunks already committed stay committed. Inspect the BatchOutcome to see what landed.
if (result.IsFailed && result.GetError() is BatchIntegrationError batchError)
{
    int committed = batchError.Outcome.Succeeded;
    IEnumerable<BatchItemResult> toRetry = batchError.Outcome.Failures;
}
```

> [!NOTE]
> The `$batch` write path (both modes) is covered by unit and integration tests but has not yet been verified against a live D365 environment; v3.0.0 ships without that run. `ContinueOnError` applies operations as individual requests — a single-`$batch` transport (`Prefer: odata.continue-on-error`) is undocumented for D365 F&O and not implemented.

**Workaround status:** inherent to D365's `$batch` operation cap. Size imports to the chunk limit and keep retries idempotent on the composite key.

## A failed batch loses its outcome when serialised

Both `Result<T>` JSON converters write each error as `code`, `message`, and `type` only, and rebuild a plain `IntegrationError` on read. A failed `Result<BatchOutcome>` that crosses a serialiser — a Durable Functions activity return, a Newtonsoft HTTP payload — comes back without its `BatchIntegrationError`, so the `Outcome` is gone. Every error's `Exception` is dropped the same way.

```csharp
// In a Durable Functions orchestrator, after the activity returned a failed batch:
Result<BatchOutcome> result = await context.CallActivityAsync<Result<BatchOutcome>>("ImportLines", lines);

IntegrationError? error = result.GetError();         // plain IntegrationError, Code "{Entity}.BatchFailed"
bool hasOutcome = error is BatchIntegrationError;    // false — the outcome did not survive
```

The per-item errors that follow the header survive as plain `IntegrationError`s: code `{Entity}.BatchOperationFailed`, message `[Index i, chunk c, HTTP s] <D365 message>`.

**Workaround status:** inherent to the shared wire shape both converters use. Read the `BatchOutcome` inside the activity and return what the orchestrator needs — for example the failed indices — as the value, or parse the per-item messages.

## Dimension Segment Delimiters

`GetDimensionOrdersQuery` reads the environment's segment delimiter from `DimensionParameters` and maps it to a `char` with `DimensionSegmentDelimiterExtensions.GetCharValue`. Only `DimensionSegmentDelimiter.Hyphen` is mapped. Every other member — `Period`, `Underscore`, `Bar`, `Tilde`, and the double-character variants — throws `ArgumentOutOfRangeException`.

The exception is not converted to a `Result`: it propagates out of `mediator.Send`, and `LoggingBehaviour` logs it at `Error` and re-throws.

```csharp
// Environment whose DimensionParameters delimiter is Period:
Result<DimensionFormat> result = await mediator.Send(
    new GetDimensionOrdersQuery("Sachkontodimensionen", DimensionHierarchyType.DataEntityLedgerDimensionFormat),
    cancellationToken);   // throws ArgumentOutOfRangeException — no failed Result comes back
```

**Workaround status:** not scheduled. Environments on the default hyphen are unaffected. For another delimiter, query `DimensionParameters` and `DimensionIntegrationFormat` yourself and split the format string on the configured delimiter.

## Deferred items

These are acknowledged and parked — no fix is scheduled:

- **D365 innererror text may reach `IntegrationError.Message`.** A downstream error body can carry server detail into the surfaced message. Treat `IntegrationError.Message` as server-authored when logging externally.
- **Non-401 failures expose `exception.Message`.** Only the auth short-circuit is sanitised to a generic reason phrase; other paths pass the exception message through.
- **Smoke-trigger handler tests absent.** The three HTTP trigger handlers behind `smoke/ledger-journal`, `smoke/financial-dimensions`, and `smoke/ledger-journal-batch` have no isolated unit coverage. The batch trigger has also not yet been run against a live D365 environment.
- **`IEntity.GetLoggingContext()` couples telemetry to the domain contract.** Splitting it into a dedicated logging interface is a MAJOR-breaking change, deferred to a future major version.

## Deprecated members awaiting removal

These members are `[Obsolete]` since v2.0.0 and still ship in v3.0.0. Removal is planned for a future major version (v4 at the earliest). Migrate off them now:

| Obsolete member | Replacement |
|---|---|
| `BaseEntity<TKey>` | Non-generic `BaseEntity` + `GetCompositeKey()` |
| `IODataService<T>.FindAll` | `FindAllAsync` |
| `IODataService<T>.QueryAsync` with a `Func`-based `orderBy` (the ordering was never applied) | The `QueryAsync` overload taking a typed `(KeySelector, Descending)` `orderBy` list |
| `ICacheableQuery.GenerateCacheKey()` / `GetCacheKeyValues()` | The `CacheKey` property |
| `ODataBatchException`, `ODataMetadataProvider` | Handled internally; no consumer replacement needed |
| `IAuthenticator` overload without a `CancellationToken` | The `CancellationToken` overload |

## See Also

- [Handle Errors](Handle-Errors)
- [Configure Resilience](Configure-Resilience)
- [Release Notes and Versioning](Release-Notes-and-Versioning)
- [Troubleshoot Common Issues](Troubleshoot-Common-Issues)
