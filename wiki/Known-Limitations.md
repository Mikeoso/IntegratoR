# Known Limitations
> Last verified against v2.0.1

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

The failure is caught before the request reaches D365, so an unsupported expression never produces a malformed call:

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

## Polly may retry HTTP 400 from APIM

The HTTP retry policy handles transient errors plus `TaskCanceledException` (`.HandleTransientHttpError().Or<TaskCanceledException>()`). APIM surfaces some gateway timeouts as HTTP 400 rather than 408 or 504, so a chronic 400 can be retried up to `RetryCount` times before the request fails.

The retries are visible, not silent — each one logs a warning to the `IntegratoR.OData.HttpRetry` logger with a rising attempt count:

```text
warn: IntegratoR.OData.HttpRetry
      HTTP retry attempt 3 after 4000ms. Reason: BadRequest
```

**Workaround status:** uninvestigated. The likely fix tightens the retry predicate — drop `Or<TaskCanceledException>` for HTTP retries (an `HttpClient.Timeout` cancellation is already 408-equivalent) or add an explicit status-code filter that excludes 400.

## `RetryCount` range 1–10 is documented, not enforced

`ODataResilienceSettings.RetryCount` defaults to `3` and its XML doc states a valid range of 1 to 10. `ODataSettingsValidator` does not check that range — it validates authentication headers, the selected mode, and its credentials only. A `RetryCount` of `0` or `50` binds and runs.

> [!CAUTION]
> Keep `RetryCount` within 1–10 yourself. A value of `0` disables retries for that policy; a large value multiplies load against D365 and delays the surfaced failure. Nothing rejects an out-of-range value at startup.

**Workaround status:** the range check is a candidate addition to `ODataSettingsValidator`; not yet implemented.

## Composite-key batch Update/Delete is per-item, not atomic

`CreateBatchCommand<T>` groups its writes into a single atomic OData changeset. `UpdateBatchCommand<T>` and `DeleteBatchCommand<T>` cannot — every D365 F&O entity is composite-keyed, and PanoramicData's changeset cannot bind a dictionary key. `ODataClientAdapter` therefore sends each composite-key update or delete as an individual HTTP request in index order.

```csharp
var updates = new List<LedgerJournalHeader> { header1, header2, header3 };
Result result = await mediator.Send(
    new UpdateBatchCommand<LedgerJournalHeader>(updates), cancellationToken);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    // Per-item, not transactional: earlier items may already be committed in D365
    // when a later item fails. Re-read to establish which writes landed.
}
```

> [!WARNING]
> A failed composite-key `UpdateBatchCommand`/`DeleteBatchCommand` is not all-or-nothing. Items before the failing one are already committed. Do not treat the batch as a transaction; re-read to reconcile, or drive idempotent per-item commands when you need precise recovery.

**Workaround status:** inherent to D365's composite-key write model. Atomic multi-entity updates would need server-side support IntegratoR cannot synthesise.

## Deferred items

These are acknowledged and parked — no fix is scheduled:

- **D365 innererror text may reach `IntegrationError.Message`.** A downstream error body can carry server detail into the surfaced message. Treat `IntegrationError.Message` as server-authored when logging externally.
- **Non-401 failures expose `exception.Message`.** Only the auth short-circuit is sanitised to a generic reason phrase; other paths pass the exception message through.
- **No Polly retry on PATCH/DELETE.** Writes are not retried — the retry policy targets idempotent reads. A transient write failure surfaces immediately.
- **Smoke-trigger handler tests absent.** The HTTP trigger handlers behind `smoke/ledger-journal` and `smoke/financial-dimensions` have no isolated unit coverage; the smoke run itself is the check.
- **`IEntity.GetLoggingContext()` couples telemetry to the domain contract.** Splitting it into a dedicated logging interface is a MAJOR-breaking change, deferred to the next MAJOR.

## Awaiting the next MAJOR

These types are `[Obsolete]` and removed in the next MAJOR. Migrate off them now:

| Obsolete member | Replacement |
|---|---|
| `BaseEntity<TKey>` | Non-generic `BaseEntity` + `GetCompositeKey()` |
| `IODataService<T>.FindAll` and the `Func`-based `QueryAsync` | Typed `QueryAsync` overloads with `Expression` filters |
| `ICacheableQuery.GenerateCacheKey()` / `GetCacheKeyValues()` | The `CacheKey` property |
| `ODataBatchException`, `ODataMetadataProvider` | Handled internally; no consumer replacement needed |
| `IAuthenticator` overload without a `CancellationToken` | The `CancellationToken` overload |

## See Also

- [Handle Errors](Handle-Errors)
- [Configure Resilience](Configure-Resilience)
- [Release Notes and Versioning](Release-Notes-and-Versioning)
- [Troubleshoot Common Issues](Troubleshoot-Common-Issues)
