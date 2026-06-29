# Known Limitations

This page lists known constraints in the framework and the planned resolutions. The goal is transparency — every limitation here has been investigated, has a documented cause, and either has a tracked workaround or has a planned fix.

> Last refreshed against v1.3.5.

## Composite-Key Write Path

**What:** `UpdateCommand<T>` and `DeleteCommand<T>` against entities with composite keys (every D365 F&O entity that includes `DataAreaId`) currently route through `PanoramicData.OData.Client`'s single-primitive-key `UpdateAsync` / `DeleteAsync` methods. The composite-key dictionary falls through to `Object.ToString()` and produces a malformed OData URL like `LedgerJournalHeaders(System.Collections.Generic.Dictionary`2[System.String,System.Object])`.

**Symptoms:**

- `UpdateCommand<LedgerJournalHeader>` returns `Result.Fail(IntegrationError("LedgerJournalHeader.NotFound", "Resource not found: LedgerJournalHeaders(System.Collections.Generic.Dictionary…)", NotFound))`.
- `DeleteCommand<LedgerJournalHeader>` returns `Result.Ok` with a `Warning` log line that says *"may indicate a malformed request URL"* — this is the framework's observability fix (since v1.3.4) surfacing the silent failure that would otherwise look like a successful delete.

**Why:** PanoramicData.OData.Client 10.0.55 has no API overload that accepts a composite key for write operations. The library's internal `ODataEntityType.Key` tracks the multiple key fields per entity, but `UpdateAsync` and `DeleteAsync` expose only `Key(object)` / `Key<TKey>(TKey)`. The library would need an upstream PR adding `Key(IDictionary<string, object>)` overloads.

**Workaround status:** Read paths (`GetByKeyQuery<T>`) already work — they bypass the limitation by constructing a `$filter` predicate. The write-path workaround is a planned raw `HttpClient` bypass in `ODataClientAdapter` that builds the composite-key URL manually for `UpdateAsync` / `DeleteAsync`. The design is fully scoped and tracked internally by maintainers. Implementation is queued behind smoke-test and dimension-related work that ships first.

**Mitigation today:** treat write-path failures as recoverable in the consumer's code. The framework's `Warning` log for suppressed-404 delete is the diagnostic signal. For one-off cleanup of orphan rows created by the LedgerJournal smoke test, use the D365 UI.

## RELion Settings Not Yet Restructured

**What:** `RelionSettings` still uses a flat structure (`ClientId`, `ClientSecret`, `TenantId`, `Resource` at the root) instead of the nested `Authentication.OAuth.*` shape introduced for `ODataSettings` in PR #76 (v1.2.0).

**Symptoms:** none — both work correctly. The inconsistency is a developer-experience issue, not a functional one.

**Workaround status:** the restructure is on the backlog. It is a breaking change for any consumer that already binds `RelionSettings` from configuration, so it will land with a clear major-version bump and a migration guide entry in [Release Notes and Versioning](Release-Notes-and-Versioning).

## `IValidateOptions<T>` Not Implemented

**What:** Settings classes (`ODataSettings`, `FOSettings`, `RelionSettings`) bind from `IConfiguration` but no `IValidateOptions<T>` is registered. Misconfiguration surfaces as either:

- A clear `ArgumentException` at first OData client resolution (when `ODataSettings.Url` is empty — the framework guards this case specifically).
- A cryptic runtime error when a missing field is dereferenced inside a handler.

**Symptoms:** for `ODataSettings.Url == ""` the error is clear (`"ODataSettings.Url must be set to a non-empty absolute URL..."`). For other missing fields the failure happens later than ideal.

**Workaround status:** on the backlog. The intended implementation registers `IValidateOptions<ODataSettings>` (and siblings) so misconfiguration fails fast at host startup with a single, comprehensive error summarising every missing field.

## Polly Retry Sometimes Retries HTTP 400

**What:** The 2026-04-14 smoke-test session observed four retries against an HTTP 400 response from APIM, despite 400 being a client error that should not retry. Root cause suspected to be `.Or<TaskCanceledException>()` in the retry predicate combined with an APIM behaviour that surfaces some timeouts as 400 rather than 408 or 504.

**Symptoms:** chronic 400-response retries log as `Warning` to the `IntegratoR.OData.HttpRetry` logger with rising retry counts before eventually failing the request. Observable but not silent.

**Workaround status:** uninvestigated. Likely candidates for the fix are tightening the retry predicate (`.HandleTransientHttpError()` alone, dropping the `Or<TaskCanceledException>` for HTTP retries since `TaskCanceledException` from `HttpClient.Timeout` is already a 408-equivalent), or adding an explicit `WhereResponseStatusCode(...)` filter.

## Entity Attribute Audit Pending

**What:** PR #92 fixed `CurrencyCode` on `LedgerJournalLine`, which had been simultaneously `[Required]` and `[ODataField(IgnoreOnCreate = true)]`. Six other fields on `LedgerJournalLine` carry the same suspect combination and have not been audited yet: `AccountDisplayValue`, `TransDate`, `DueDate`, `DocumentDate`, `ExchRate`, `ReverseDate`.

**Symptoms:** none observed today on the smoke-test path. The bug shape is *silent payload exclusion of a required field*, which D365 may accept (computing a default) or reject (HTTP 400 from D365 with a server-side validation message) depending on the journal template and field.

**Workaround status:** queued. Each field needs verification against D365's metadata — is the value server-generated (keep `IgnoreOnCreate`) or consumer-supplied (drop the attribute)? Each fix lands with a reflection-based regression test pinning the attribute state.

## Cross-Company Queries Not Surfaced

**What:** D365 OData supports a `cross-company=true` query parameter that returns rows across all legal entities the service principal can read. The framework does not surface this directly — every query is implicitly scoped to the company set by `DataAreaId` in the entity composite key.

**Symptoms:** N/A — multi-company queries require splitting into per-company calls today.

**Workaround status:** not yet planned. The right shape is probably a per-query opt-in marker interface (`ICrossCompanyQuery`) handled by a dedicated pipeline behaviour, but the design has not been worked out.

## OData `$expand` Translator Coverage

**What:** the LINQ-to-OData translator supports filter, select, and expand expressions plus `Any` / `All` lambdas. Some advanced `$expand` shapes (nested expand with filter, expand with select inside an Any) are not yet covered and throw `NotSupportedException` at translation time.

**Symptoms:** caught at translation time, not at runtime — the failing expression never reaches D365. Tests under `tests/IntegratoR.OData.Tests/Common/Filters/IntegratoRODataExpressionTranslatorTests.cs` document the supported shapes.

**Workaround status:** add coverage on demand. The translator is intentionally narrow — favouring predictable D365-compatible output over comprehensive LINQ coverage.

## Where to Track Progress

The framework's backlog, tracked internally by maintainers, consolidates these items and tracks which has an active design and which is still scoped. Items shipping in a given release are noted in [Release Notes and Versioning](Release-Notes-and-Versioning) with the relevant PR link.

## See Also

- [Send Commands](Send-Commands) — composite-key Update/Delete limitation referenced from the command pages
- [Run Smoke Tests](Run-Smoke-Tests) — the smoke tests that surface the composite-key write issue
- [Release Notes and Versioning](Release-Notes-and-Versioning) — when fixes ship
- [Troubleshoot Common Issues](Troubleshoot-Common-Issues) — the operator-side view of the same incidents
