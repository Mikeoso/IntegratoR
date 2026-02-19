# OData Client Strategy: Multi-Perspective Analysis

> **Date:** 2026-02-18
> **Status:** Analysis Complete
> **Analysts:** Senior Software Engineer, Security & Stability Expert, Pragmatic Technical Lead
> **Scope:** Replace Simple.OData.Client 6.0.1 with PanoramicData.OData.Client in IntegratoR

---

## 1. Context

Simple.OData.Client 6.0.1 is the primary data transport layer for all D365 F&O and RELion OData operations in IntegratoR. The library is **explicitly unmaintained** (README states this, last commit May 2024). Its dependency chain locks `Microsoft.OData.Core` to `< 8.0` and pulls in legacy OData v3 packages from 2018.

The user considered building a custom OData client but, after the first round of expert analysis, wants to evaluate **PanoramicData.OData.Client** as the replacement — an actively maintained spiritual successor to Simple.OData.Client by the same contributors.

---

## 2. Architectural Foundation

**Simple.OData.Client touches exactly 3 files** in the entire codebase:

| File | Lines | Types Used |
|------|-------|-----------|
| `IntegratoR.OData\Common\Services\ODataService.cs` | 372 | `IODataClient`, `ODataBatch`, fluent API (For, Key, Set, Filter, Expand, Select, Skip, Top, Count, all CRUD async methods) |
| `IntegratoR.OData\Common\Services\ODataExceptionHandler.cs` | 395 | `WebRequestException` with `.Code` (HttpStatusCode) |
| `IntegratoR.OData\Common\Extensions\ApplicationDependencyInjection.cs` | 225 | `ODataClientSettings`, `ODataClient`, `ODataPayloadFormat`, `IODataClient`, `WebRequestException` |

All downstream consumers use IntegratoR-owned interfaces: `IService<TEntity>`, `IODataService<TEntity>`, `IODataBatchService<TEntity>`. This textbook dependency inversion means any replacement is **surgically scoped**.

---

## 3. Why Not a Custom OData Client

All three experts unanimously reject this option:

| Factor | Assessment |
|--------|-----------|
| Realistic effort | 16-27 weeks (not 10-16 initially estimated) |
| Hardest component | LINQ-to-`$filter` translator: 5-8 weeks, ~50 classes in Simple.OData.Client |
| Second hardest | `$batch` multipart MIME + D365-specific error payloads: 2-4 weeks |
| Hidden cost | Integration testing against live D365 F&O: 2-4 weeks (D365 quirks: 30-80MB metadata, decimal suffixes, composite key encoding, varying error formats) |
| Business value | Zero — replicates existing functionality |
| 3-year TCO | 26-43 weeks (build + permanent maintenance) |

> "Engineering vanity masked as strategic investment." — Pragmatic Lead

---

## 4. PanoramicData.OData.Client: Deep Analysis

### 4.1 Library Profile

| Attribute | Value |
|-----------|-------|
| Version | 10.0.55 (Feb 2025) |
| Target | `net10.0` native |
| Dependencies | Only `Microsoft.Extensions.Logging.Abstractions >= 10.0.1` |
| Downloads | ~7,700 total (540 current version) |
| Commits | 59 |
| License | MIT |
| Maintainer | Panoramic Data Ltd (UK, est. 2009, ISO 27001:2022 certified) |
| Relation | Spiritual successor to Simple.OData.Client by same contributors |

### 4.2 API Mapping

| Operation | Simple.OData.Client | PanoramicData.OData.Client |
|-----------|---------------------|---------------------------|
| Create | `For<T>().Set(payload).InsertEntryAsync(true, ct)` | `CreateAsync<T>(entitySet, entity, null, ct)` |
| Read single | `For<T>().Key(keys).FindEntryAsync(ct)` | `For<T>(entitySet).Key(keys).GetFirstOrDefaultAsync(ct)` |
| Read many | `For<T>().Filter(expr).FindEntriesAsync(ct)` | `For<T>(entitySet).Filter(expr/string).GetAsync(ct)` |
| Update | `For<T>().Key(entity).Set(payload).UpdateEntryAsync(ct)` | `UpdateAsync<T>(entitySet, key, values, ct)` |
| Delete | `For<T>().Key(entity).DeleteEntryAsync(ct)` | `DeleteAsync<T>(entitySet, key, ct)` |
| Count | `For<T>().Filter(expr).Count().FindScalarAsync<int>(ct)` | `For<T>(entitySet).Filter(string).GetCountAsync(ct)` returns `long` |
| Filter | `Expression<Func<T, bool>>` | **Both** `Expression<Func<T, bool>>` and `string` |
| Expand | `Expression<Func<T, object>>` | **Both** `Expression<Func<T, object?>>` and `string` |
| Select | `Expression<Func<T, object>>` | **Both** `Expression<Func<T, object?>>` and `string` |
| OrderBy | `Expression<Func<T, object>>` | `string` only |
| Batch | `ODataBatch` with `+=` operator + `ExecuteAsync()` | `ODataBatchBuilder` with method chaining |
| Config | `ODataClientSettings(httpClient)` | `ODataClientOptions { HttpClient = ... }` |
| Exceptions | `WebRequestException` with `.Code` | Typed hierarchy: `ODataClientException`, `ODataNotFoundException`, `ODataUnauthorizedException`, `ODataForbiddenException`, `ODataConcurrencyException` |

### 4.3 Key Discovery: LINQ Expression Support

**Contrary to initial analysis, PanoramicData DOES support LINQ expressions.** The library has a built-in expression parser (`ODataQueryBuilder.ExpressionParsing.cs` + `ODataQueryBuilder.LambdaParsing.cs`) that handles:

```csharp
.Filter(Expression<Func<T, bool>> predicate)
.Select(Expression<Func<T, object?>> selector)
.Expand(Expression<Func<T, object?>> selector)
```

This eliminates the "central migration challenge" that originally estimated 5-8 weeks. The interfaces can potentially be preserved as-is.

### 4.4 Critical Landmine: Property Name Resolution

**PanoramicData's expression parser uses CLR property names, not `[JsonPropertyName]` values.**

Example from `LedgerJournalHeader`:
```csharp
[JsonPropertyName("dataAreaId")]  // OData expects: dataAreaId
public required string DataAreaId { get; set; }  // CLR name: DataAreaId
```

The expression `x => x.DataAreaId == "USMF"` would produce `$filter=DataAreaId eq 'USMF'` when D365 F&O expects `$filter=dataAreaId eq 'USMF'`. **This will break for any entity where CLR name != OData property name.**

**Solutions (ranked by pragmatism):**

1. **Configure `JsonNamingPolicy.CamelCase`** on `ODataClientOptions.JsonSerializerOptions` — if PanoramicData uses this for expression resolution, it fixes `DataAreaId` → `dataAreaId` globally. Must verify in spike.
2. **Contribute to PanoramicData** — add `[JsonPropertyName]` attribute support in `GetMemberPath()`. Surgical change, MIT-licensed, active maintainer.
3. **Rename CLR properties** to match OData names — ugly, violates C# conventions, large ripple.
4. **Switch to string-based filters** — nuclear option, ripples through all interfaces and consumers.

### 4.5 Additional Gaps Identified

| Gap | Impact | Mitigation |
|-----|--------|------------|
| **No `MetadataDocument` property** | Cannot inject pre-loaded metadata XML. D365 F&O `$metadata` is ~50MB. Cold start regression. | Cache metadata at HTTP level, or adapt `ODataMetadataProvider` to feed PanoramicData |
| **No `IgnoreUnmappedProperties`** | D365 returns unmapped properties. If deserializer throws `JsonException`, this is a hard blocker. | Configure `JsonSerializerOptions` or add `[JsonExtensionData]` to entities |
| **No `ReadUntypedAsString`** | D365 `Edm.Untyped` values may deserialize as `JsonElement` instead of string | Verify in spike; may need custom converter |
| **Composite key via `Key<TKey>(TKey key)`** | Takes single generic, not `object[]`. IntegratoR uses `GetCompositeKey()` → `object[]`. | Build helper to format composite keys into OData URL syntax |
| **`GetCountAsync` returns `long`** | Interface returns `Result<int>` | Cast with overflow check |
| **`OrderBy(string)` only** | `IODataService.QueryAsync` has `Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>?` parameter | Currently unused (0 callers). Remove or adapt to string. |
| **Batch returns per-operation results** | Current code assumes single exception on batch failure | Richer error reporting but requires handler restructuring |

---

## 5. Security Assessment

### 5.1 Dependency Chain Improvement

| Metric | Before (Simple.OData.Client) | After (PanoramicData) |
|--------|-----|------|
| Direct NuGet packages on OData layer | 4 | 1 |
| Legacy/EOL transitive packages | 3 (Microsoft.Data.OData 5.8.5, Microsoft.Data.Edm 5.8.5, System.Spatial 5.8.5) | 0 |
| Version ceiling locks | 1 (Microsoft.OData.Core < 8.0) | 0 |
| Process-wide `AppContext.SetSwitch` needed | Yes | No |

**The migration produces a clear, measurable security improvement** in dependency hygiene.

### 5.2 Maturity Risk

| Factor | Simple.OData.Client | PanoramicData |
|--------|---------------------|---------------|
| Downloads | 7.3M total | 7,700 total |
| Commits | 1,734 | 59 |
| D365 F&O battle-tested | Years of production use | Unproven at scale |
| Maintenance | Abandoned | Active (ISO 27001 company) |

**This is the migration's single largest risk.** The risk profile shifts from "known vulnerabilities in dead dependencies" to "unknown unknowns in young dependency."

### 5.3 Concrete Security Improvements

1. **Eliminates `AppContext.SetSwitch("Switch.System.Xml.AllowDefaultResolver", true)`** — process-wide XML security improvement
2. **Removes legacy OData v3 attack surface** — 3 deprecated packages eliminated
3. **Unlocks Microsoft.OData.Core upgrades** — no more `< 8.0` ceiling
4. **System.Text.Json for OData path** — more secure default serializer (no `TypeNameHandling` attack surface)
5. **Typed exceptions** — more precise error handling, reduced misclassification risk

### 5.4 Security Hardening (Must-Do)

| Priority | Action |
|----------|--------|
| P0 | Remove `AppContext.SetSwitch` from `ApplicationDependencyInjection.cs:56` |
| P0 | Enable `CentralPackageTransitivePinningEnabled = true` in `Directory.Packages.props:5` |
| P0 | Validate PanoramicData batch operations against D365 F&O sandbox (50+ entities) |
| P1 | Consolidate retry layers — PanoramicData has built-in retry; dual Polly + built-in = triple retry amplification on non-idempotent writes |
| P1 | Strip `RequestUrl` from exception logs (contains business-sensitive composite key values) |
| P1 | Remove internal `ODataNotFoundException` (lines 391-394 of `ODataExceptionHandler.cs`) — use PanoramicData's native type |
| P2 | Pin PanoramicData version with tested-version comment |
| P2 | Monitor PanoramicData GitHub for security advisories |

---

## 6. Migration Effort Estimate

### 6.1 Phase Breakdown

| Phase | Effort | Gate |
|-------|--------|------|
| **Phase 0: Spike** | 2-3 days | **Pass/fail decision.** Must verify: composite keys vs D365, LINQ expressions with property name mismatch, enum values in filters, unmapped properties in responses, batch operations. |
| **Phase 1a: Integration tests** | 3-5 days | Write tests for existing `ODataService<TEntity>` against D365 sandbox. Covers all 15 API methods. Safety net for migration AND future work. |
| **Phase 1b: Entity set name resolver** | <1 day | PanoramicData uses naive pluralization. Build helper that reads `[Table]` attribute. |
| **Phase 2: ODataService.cs rewrite** | 3-5 days | Every CRUD, query, and batch method body changes. Payload building with `ODataFieldAttribute` preserved. |
| **Phase 3: ODataExceptionHandler.cs** | 1-2 days | `WebRequestException` switch → type-based dispatch for PanoramicData hierarchy. |
| **Phase 4: ApplicationDependencyInjection.cs** | 2-3 days | Client construction, metadata strategy, retry consolidation. |
| **Phase 5: Fix issues from testing** | 2-3 days | Always underestimated. D365 edge cases. |
| **Phase 6: Interface changes** | 0-2 days | Only if spike reveals LINQ property name resolution cannot be configured. |
| **Phase 7: End-to-end staging** | 2-3 days | Full orchestrator flows against D365 sandbox. |
| **Total** | **15-23 developer-days (3-5 calendar weeks)** | |

### 6.2 Files That Must Change

| File | Change Scope |
|------|-------------|
| `IntegratoR.OData\Common\Services\ODataService.cs` | Every method body (372 lines) |
| `IntegratoR.OData\Common\Services\ODataExceptionHandler.cs` | Exception type mapping (395 lines) |
| `IntegratoR.OData\Common\Extensions\ApplicationDependencyInjection.cs` | Client construction, retry policies (225 lines) |
| `IntegratoR.OData\IntegratoR.OData.csproj` | Package reference swap |
| `Directory.Packages.props` | Package version swap + enable transitive pinning |

### 6.3 Files That May Change (Conditional on Spike)

| File | Condition |
|------|-----------|
| `IntegratoR.Abstractions\Interfaces\Services\IService.cs` | Only if LINQ expressions cannot work |
| `IntegratoR.OData\Interfaces\Services\IODataService.cs` | Only if LINQ expressions cannot work |
| `IntegratoR.Abstractions\Common\CQRS\Queries\GetByFilterQuery.cs` | Only if switching to string filters |
| All query handlers (6 files with LINQ filter call sites) | Only if `FindAsync` signature changes |

### 6.4 Files That Will NOT Change

| File | Reason |
|------|--------|
| All 4 batch command handlers | Use `IODataBatchService<TEntity>` interface only |
| All orchestrators | Call MediatR, no direct OData access |
| All activity functions | Call MediatR handlers |
| All entity models | `[JsonPropertyName]`, `[Table]`, `[Key]` still valid |
| `ODataAuthenticationHandler.cs` | Pure HTTP handler, no OData client reference |
| `ODataMetadataProvider.cs` | Reads XML files, no OData client reference |
| `RelionService.cs` | Uses raw HttpClient, not OData client |

---

## 7. Risk Mitigation

### 7.1 The Spike is Non-Negotiable (Phase 0)

The spike must verify these 5 items against a real D365 F&O environment:

1. **Composite key CRUD** — `GetByKeyAsync` with `[DataAreaId, JournalBatchNumber]`
2. **LINQ expression property names** — `x => x.DataAreaId == "USMF"` must produce `dataAreaId eq 'USMF'`
3. **Enum values in filters** — `x => x.IsActive == NoYes.Yes` must produce valid D365 filter syntax
4. **Unmapped properties in responses** — D365 returns dozens of fields not in entity models
5. **Batch operations** — `ODataBatchBuilder` with 5+ create operations, verify atomicity

**If the spike fails on items 2, 3, or 4, stop. Either contribute fixes to PanoramicData or shelve the migration.**

### 7.2 No Tests = No Migration

There are zero test projects in IntegratoR. Write integration tests BEFORE migrating. Target `ODataService<TEntity>` directly:
- `AddAsync` with `LedgerJournalHeader`
- `GetByKeyAsync` with composite key
- `FindAsync` with LINQ expression including enum comparison
- `UpdateAsync` + verify
- `DeleteAsync`
- `AddBatchAsync` with 5+ entities
- `CountAsync` with and without filter

These tests become the safety net for the migration and serve both old and new client.

### 7.3 Feature Flag for Rollback

Register both implementations behind a configuration switch. DI resolves to the correct `ODataService<T>` at startup. Rollback = change a config value. No redeployment needed.

### 7.4 Sequencing

```
Phase 0: Spike (2-3 days) ──────── GATE: pass/fail ──────────────┐
                                                                   │
Phase 1a: Integration tests (3-5 days) ─┐                         │
Phase 1b: Entity set resolver (<1 day) ─┤ parallel                │
                                         │                         │
Phase 2: ODataService.cs (3-5 days) ────┘                         │
Phase 3: ExceptionHandler (1-2 days) ── depends on Phase 2        │
Phase 4: DI registration (2-3 days) ─── depends on Phase 3        │
Phase 5: Fix issues (2-3 days)                                    │
Phase 6: Interface changes (0-2 days) ── only if spike flagged ◄──┘
Phase 7: E2E staging (2-3 days)
```

---

## 8. Existing Security Findings (Independent of Migration)

These should be addressed regardless of OData client strategy:

| Finding | Severity | Location |
|---------|----------|----------|
| Process-wide `AllowDefaultResolver` XML switch | Medium | `ApplicationDependencyInjection.cs:56` |
| Retry amplification on non-idempotent writes (HTTP Polly x OData Polly = up to 9 attempts) | High | `ApplicationDependencyInjection.cs:66-190` |
| `CentralPackageTransitivePinningEnabled = false` | Medium | `Directory.Packages.props:5` |
| Legacy OData v3 transitive deps (dead weight + attack surface) | Low-Medium | Via Simple.OData.Client |
| MSAL `IConfidentialClientApplication` not reused as singleton | Low | `OAuthAuthenticator.cs` — new instance per token request bypasses MSAL cache |

---

## 9. Expert Verdicts Summary

### Senior Engineer
> The migration is mechanically straightforward: 3 files change, interfaces potentially preserved. The property name resolution in LINQ expressions is the critical unknown — verify in spike, contribute fix if needed. The `MetadataDocument` gap is real but solvable.

### Security Expert
> Net security improvement. Eliminates 3 legacy packages, the version ceiling lock, and the process-wide XML switch. The maturity gap (7,700 vs 7.3M downloads) is the primary risk, mitigated by IntegratoR's architecture (local metadata, Result pattern, validation pipeline). Panoramic Data is a legitimate, ISO 27001-certified company.

### Pragmatic Lead
> 15-23 developer-days total. The spike (2-3 days) is the only responsible first step. If it passes, proceed with tests first, then migration. If it reveals blockers, either contribute fixes upstream or shelve. Do NOT start migration without integration tests — you're replacing the core data transport with zero test coverage.

---

## 10. Recommended Path Forward

1. **Do the spike** (Phase 0, 2-3 days) — this is the decision gate
2. **Write integration tests** (Phase 1, 3-5 days) — prerequisite regardless
3. **Migrate** (Phases 2-5, 8-13 days) — scoped to 3 core files + DI
4. **Validate in staging** (Phase 7, 2-3 days) — full orchestrator flows
5. **Deploy with feature flag** — instant rollback capability

**Total: 15-23 developer-days (3-5 calendar weeks)**

If the spike fails, fall back to **forking Simple.OData.Client** (1-2 days, zero code changes, full dependency control).
