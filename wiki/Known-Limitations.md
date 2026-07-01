# Known Limitations

This page lists known constraints in the framework and the planned resolutions. The goal is transparency — every limitation here has been investigated, has a documented cause, and either has a tracked workaround or has a planned fix.

> Last refreshed against v2.0.1.

## Composite-Key Write Path — RESOLVED

**Status:** resolved. `UpdateCommand<T>` and `DeleteCommand<T>` against entities with composite keys (every D365 F&O entity that includes `DataAreaId`) now write correctly.

**What was wrong:** writes previously routed through `PanoramicData.OData.Client`'s single-primitive-key `UpdateAsync` / `DeleteAsync` methods. The composite-key dictionary fell through to `Object.ToString()` and produced a malformed OData URL like `LedgerJournalHeaders(System.Collections.Generic.Dictionary`2[System.String,System.Object])`, so updates returned `*.NotFound` and deletes silently no-opped.

**How it is fixed:** `ODataClientAdapter` detects a composite key (an `IDictionary<string, object>`) and issues the PATCH / DELETE through an owned raw-`HttpClient` bypass that builds the keyed URL manually — `LedgerJournalHeaders(dataAreaId='USMF',JournalBatchNumber='B0001')` — sent through the named `"ODataClient"` client so it carries the same authentication, Polly resilience, and `BaseAddress` as every other request. This mirrors the long-standing read-path bypass used by `GetByKeyQuery<T>`. The bypass is owned, first-party source maintained in this repository (not a fork awaiting upstream).

**Coverage:** adapter-level unit tests pin the keyed URL construction, value formatting (string / Guid / enum / `DateOnly`), and the 404-treated-as-success delete path; the LedgerJournal smoke test exercises the create → update → re-read → delete → verify-gone round-trip end to end.

## `IValidateOptions<T>` Not Implemented

**What:** Settings classes (`ODataSettings`, `FOSettings`) bind from `IConfiguration` but no `IValidateOptions<T>` is registered. Misconfiguration surfaces as either:

- A clear `ArgumentException` at first OData client resolution (when `ODataSettings.Url` is empty — the framework guards this case specifically).
- A cryptic runtime error when a missing field is dereferenced inside a handler.

**Symptoms:** for `ODataSettings.Url == ""` the error is clear (`"ODataSettings.Url must be set to a non-empty absolute URL..."`). For other missing fields the failure happens later than ideal.

**Workaround status:** on the backlog. The intended implementation registers `IValidateOptions<ODataSettings>` (and siblings) so misconfiguration fails fast at host startup with a single, comprehensive error summarising every missing field.

## Polly Retry Sometimes Retries HTTP 400

**What:** The 2026-04-14 smoke-test session observed four retries against an HTTP 400 response from APIM, despite 400 being a client error that should not retry. Root cause suspected to be `.Or<TaskCanceledException>()` in the retry predicate combined with an APIM behaviour that surfaces some timeouts as 400 rather than 408 or 504.

**Symptoms:** chronic 400-response retries log as `Warning` to the `IntegratoR.OData.HttpRetry` logger with rising retry counts before eventually failing the request. Observable but not silent.

**Workaround status:** uninvestigated. Likely candidates for the fix are tightening the retry predicate (`.HandleTransientHttpError()` alone, dropping the `Or<TaskCanceledException>` for HTTP retries since `TaskCanceledException` from `HttpClient.Timeout` is already a 408-equivalent), or adding an explicit `WhereResponseStatusCode(...)` filter.

## Entity Attribute Audit Pending

**Fixed in v2.0.1 (`LedgerJournalHeader` read-only-on-update fields):** the live 2026-07-01 JFI run found D365 rejects the entire update PATCH with an `ODataSecurityException` (HTTP 403) whenever a read-only field rides in the payload. `LedgerJournalHeader`'s `JournalName`, `AccountingCurrency`, `IsPosted`, `JournalTotalDebit`, and `JournalTotalCredit` are now `[ODataField(IgnoreOnUpdate = true)]`, so composite-key `UpdateCommand<LedgerJournalHeader>` succeeds.

**Still open — `LedgerJournalLine` `[Required]` + `IgnoreOnCreate` audit:** PR #92 fixed `CurrencyCode` on `LedgerJournalLine`, which had been simultaneously `[Required]` and `[ODataField(IgnoreOnCreate = true)]`. Six other fields on `LedgerJournalLine` carry the same suspect combination and have not been audited yet: `AccountDisplayValue`, `TransDate`, `DueDate`, `DocumentDate`, `ExchRate`, `ReverseDate`.

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

- [Send Commands](Send-Commands) — composite-key Update/Delete, now via the owned write bypass
- [Run Smoke Tests](Run-Smoke-Tests) — the smoke tests that verify composite-key writes work end to end
- [Release Notes and Versioning](Release-Notes-and-Versioning) — when fixes ship
- [Troubleshoot Common Issues](Troubleshoot-Common-Issues) — the operator-side view of the same incidents
