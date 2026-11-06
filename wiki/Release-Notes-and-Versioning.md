# Release Notes and Versioning
> Last verified against v3.0.0

GitVersion computes every version in **ContinuousDelivery** mode: each merge to `main` produces a `X.Y.Z-ci.N` pre-release on NuGet, and a maintainer promotes a chosen build to a stable `X.Y.Z`. GitVersion defaults to a **PATCH** bump and ignores conventional-commit prefixes — signal a MINOR or MAJOR with a `+semver:` marker. Public API is deprecated for at least one MINOR before removal. The exhaustive log lives in [CHANGELOG.md](https://github.com/Mikeoso/IntegratoR/blob/main/CHANGELOG.md) and [GitHub Releases](https://github.com/Mikeoso/IntegratoR/releases); this page keeps a curated table and the per-MAJOR migration guides.

## How versions are produced

| Trigger | Version produced | NuGet listing |
|---|---|---|
| Push to `main` (every squash-merge) | `X.Y.Z-ci.N` pre-release | Pre-release, hidden by default |
| `gh workflow run publish.yml -f release=true` on `main` | `X.Y.Z` stable | Stable, shown by default |
| The same dispatch on a `support/<major>.<minor>` branch | `X.Y.Z` hotfix on an older line | Stable, never marked the latest GitHub Release |

All publish to `nuget.org`. Pin an exact stable version (the latest stable is `3.0.0`); use a `3.0.*-*` range to track the latest pre-release. Tags are created by the publish workflow — never tag manually. A support branch publishes only on that explicit dispatch, never on push.

> [!CAUTION]
> GitVersion produces a PATCH bump on every merge (`3.0.0 → 3.0.1 → …`); `feat:` / `fix:` prefixes do **not** change this. To ship a MINOR or MAJOR, add `+semver: minor` or `+semver: major` to the merge commit message, or set `next-version:` in `GitVersion.yml` before merging.

## Pre-release vs stable

Consume a pre-release to test a fix against a sandbox before promotion:

```bash
dotnet add package IntegratoR.Hosting --version 3.0.1-ci.5
```

A `3.0.1-ci.N` build is immutable — the next push produces `3.0.1-ci.N+1`, never a silent update. Pin the stable version for production.

## Deprecate before remove

A public type is marked `[Obsolete]` for at least one MINOR before it is removed in a later MAJOR. Deprecated since v2.0.0 and still shipped in v3.0.0, with removal planned for a future major version (v4 at the earliest): `BaseEntity<TKey>`, `IODataService.FindAll` (use `FindAllAsync`), the `Func`-based `IODataService.QueryAsync` overload, `ODataBatchException`, `ICacheableQuery.GenerateCacheKey` / `GetCacheKeyValues`, `ODataMetadataProvider`, and the `IAuthenticator` overload without a `CancellationToken`. Derive from the non-generic `BaseEntity` now — see [Upgrading to v2](#upgrading-to-v2) below and the replacement table in [Known Limitations](Known-Limitations#deprecated-members-awaiting-removal).

## Released Versions

| Version | Highlights |
|---|---|
| [v3.0.0](https://github.com/Mikeoso/IntegratoR/releases/tag/v3.0.0) | **Breaking.** Configurable, chunked `$batch` writes: batch commands and services return `Result<BatchOutcome>`; `BatchFailureMode` (`Atomic` / `ContinueOnError`) and automatic chunking under `ODataSettings.Batch`; key values with control characters and invalid entity-set names rejected; an unreconcilable failed changeset reports `502`. The `$batch` path is unit- and integration-tested but not yet verified against live D365. See [Upgrading to v3](#upgrading-to-v3). |
| [v2.0.1](https://github.com/Mikeoso/IntegratoR/releases/tag/v2.0.1) | Generic command validation fires through the pipeline; `UpdateAsync` returns the entity on a `204` composite-key PATCH; `LedgerJournalHeader` read-only fields marked `IgnoreOnUpdate`; null-safe `GetLoggingContext()`. |
| [v2.0.0](https://github.com/Mikeoso/IntegratoR/releases/tag/v2.0.0) | **Breaking.** Composite-key writes (Update/Delete/batch) live; typed `$orderby`; `ODataSettingsValidator`; `IBatchService<T>` + generic batch handlers; 401/403 + MSAL leak fixes. See [Upgrading to v2](#upgrading-to-v2). |
| [v1.3.5](https://github.com/Mikeoso/IntegratoR/releases/tag/v1.3.5) | FinancialDimension smoke test + dimension query fixes; enum qualified-type form in `Any`/`All` lambda bodies. |
| [v1.3.4](https://github.com/Mikeoso/IntegratoR/releases/tag/v1.3.4) | Cross-assembly handler closing; `BaseAddress` trailing-slash normalisation; `LedgerJournalLine` `CurrencyCode` payload fix. |
| [v1.3.3](https://github.com/Mikeoso/IntegratoR/releases/tag/v1.3.3) | `[JsonPropertyName]`-aware OData filter / select / expand translator — camelCase wire names honoured throughout the LINQ path. |
| [v1.2.0](https://github.com/Mikeoso/IntegratoR/releases/tag/v1.2.0) | **Breaking.** `ODataSettings` restructure — OAuth and resilience moved under `Authentication` / `Resilience` sub-objects. |
| [v1.1.0](https://github.com/Mikeoso/IntegratoR/releases/tag/v1.1.0) | Initial public release. |

For the full per-commit history, run `git log v3.0.0..HEAD --oneline` (or `git log v2.0.1..v3.0.0` for any two adjacent tags).

## Upgrading to v3

v3.0.0 redesigns batch writes ([ADR-0004](https://github.com/Mikeoso/IntegratoR/blob/main/docs/adr/0004-configurable-chunked-odata-batch.md)). Code that sends batch commands, implements `IBatchService<T>` or `IODataClientAdapter`, or constructs `ODataService<T>` directly needs changes; single-entity commands and queries compile unchanged.

### Batch commands return `Result<BatchOutcome>`

`CreateBatchCommand<T>`, `UpdateBatchCommand<T>`, and `DeleteBatchCommand<T>` — and the F&O `…HeadersCommand<T>` / `…LinesCommand<T>` records deriving from them — return `Result<BatchOutcome>` instead of `Result`. The generic commands also gain an optional `BatchOptions? Options` parameter.

**Before (v2.x):** `Result result = await mediator.Send(new CreateBatchCommand<LedgerJournalLine>(lines), cancellationToken);`

**After:**

```csharp
Result<BatchOutcome> result = await mediator.Send(
    new CreateBatchCommand<LedgerJournalLine>(lines), cancellationToken);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();

    if (error is BatchIntegrationError batchError)
    {
        // The batch ran: batchError.Outcome.Failures lists Index, ChunkIndex, Key, StatusCode, ErrorMessage.
    }
    // Otherwise it was rejected before sending: Validation.Error, {Entity}.InvalidKey, {Entity}.KeyCountMismatch.
}
```

Change the declared type to `Result<BatchOutcome>`. See [Send Commands](Send-Commands#send-a-batch).

### Default batch behaviour changed

In v2, a batch create was one changeset for the whole list, and a composite-key batch update or delete was sent as one request per item. In v3 every batch defaults to `Atomic`: one changeset per chunk of 150 operations.

- A batch create of more than 150 items is now several changesets, each committing on its own.
- A composite-key batch update or delete is now all-or-nothing per chunk — one bad item rolls back its whole chunk, where v2 committed the others.

To keep v2's per-item behaviour for updates and deletes, pass `new BatchOptions { Mode = BatchFailureMode.ContinueOnError }` or set `ODataSettings.Batch.FailureMode` to `ContinueOnError`. See [Configure OData](Configure-OData#batch-options).

### `IBatchService<T>` takes `BatchOptions`

`AddBatchAsync`, `UpdateBatchAsync`, and `DeleteBatchAsync` on `IBatchService<T>` / `IODataBatchService<T>` return `Result<BatchOutcome>` and gained `BatchOptions? options = null` before the `CancellationToken`. Implementors must update the signatures; a caller passing the token positionally must name it.

**Before:** `await service.AddBatchAsync(lines, cancellationToken);`

**After:** `await service.AddBatchAsync(lines, cancellationToken: cancellationToken);`

### `IODataClientAdapter` batch methods take a `BatchFailureMode`

`BatchCreateAsync`, `BatchUpdateAsync`, and `BatchDeleteAsync` gained a required `BatchFailureMode mode` parameter before the `CancellationToken`; the return type is unchanged. Each call sends exactly the items it is given — chunking happens in `ODataService`. This breaks implementors and direct callers; consumers going through `IMediator` are unaffected.

**After:** `await adapter.BatchDeleteAsync("LedgerJournalLines", keys, BatchFailureMode.Atomic, cancellationToken);`

### `ODataService<T>` constructor gained an optional parameter

The public constructor gained `IOptions<ODataSettings>? settings = null`, which supplies the batch defaults. Source compiles unchanged, but it is a binary break: recompile anything that constructs `ODataService<T>` directly. DI-based consumers are unaffected.

### New input rejection and the `502` status

- **Control characters in keys.** `IODataClientAdapter`'s batch methods and its composite-key `UpdateAsync` / `DeleteAsync` throw `ArgumentException` for a key value containing a control character; the batch methods also throw for an entity-set name that is not a plain OData identifier. Through `ODataService`, a batch update or delete returns `{Entity}.InvalidKey` (`Validation`) before any chunk is sent, and a single composite-key update or delete returns `{Entity}.UnexpectedError`.
- **Unreconcilable changesets.** An atomic changeset that did not commit and whose sub-responses cannot be matched to the operations sent reports `StatusCode` 502 for every item, instead of a borrowed 2xx.

## Upgrading to v2

v2.0.0 carries three source-breaking API changes and one deprecation. Each has a mechanical fix; the migration is contained to code that calls these members directly. All other consumer code compiles unchanged.

### `BaseEntity<TKey>` → non-generic `BaseEntity`

The `TKey` parameter was never used. Derive from the non-generic `BaseEntity` and override `GetCompositeKey()` to return the key parts in composite-key order. `BaseEntity<TKey>` is `[Obsolete]` since v2.0.0; removal is planned for a future major version (v4 at the earliest).

**Before (v1.x):**

```csharp
[Table("LedgerJournalHeaders")]
public sealed class LedgerJournalHeader : BaseEntity<string>
{
    [Key]
    [JsonPropertyName("dataAreaId")]
    public required string DataAreaId { get; set; }

    [Key]
    [ODataField(IgnoreOnCreate = true)]
    public string JournalBatchNumber { get; set; } = string.Empty;
}
```

**After (v2.x):**

```csharp
[Table("LedgerJournalHeaders")]
public sealed class LedgerJournalHeader : BaseEntity
{
    [Key]
    [JsonPropertyName("dataAreaId")]
    public required string DataAreaId { get; set; }

    [Key]
    [ODataField(IgnoreOnCreate = true)]
    public string JournalBatchNumber { get; set; } = string.Empty;

    public override object[] GetCompositeKey() => [DataAreaId, JournalBatchNumber];
}
```

Change the base class and add the `GetCompositeKey()` override. See [Define Entities](Define-Entities) for the full attribute set.

### Batch commands take `IReadOnlyList<T>`

`CreateBatchCommand<T>`, `UpdateBatchCommand<T>`, and `DeleteBatchCommand<T>` (and the F&O records deriving from them) now take `IReadOnlyList<T>` instead of `IEnumerable<T>`, removing multiple-enumeration and giving an O(1) count.

**Before:** `new CreateBatchCommand<LedgerJournalHeader>(headers.Where(h => h.DataAreaId == "USMF"))`

**After:**

```csharp
IReadOnlyList<LedgerJournalHeader> headers =
    source.Where(h => h.DataAreaId == "USMF").ToList();

// v2.x returned Result; since v3.0.0 the result is Result<BatchOutcome> — see Upgrading to v3.
Result<BatchOutcome> result = await mediator.Send(
    new CreateBatchCommand<LedgerJournalHeader>(headers), cancellationToken);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    // Validation.Error for an empty list, or a BatchIntegrationError listing the headers that failed.
}
```

Materialise the sequence with `.ToList()` (or `.ToArray()`) before constructing the command. `JournalBatchNumber` is `IgnoreOnCreate`, so the framework strips it from each create payload and D365 assigns it.

### `GetDimensionOrdersQuery` parameters are PascalCase

The positional record parameters were renamed `dimensionFormat` / `hierarchyType` → `DimensionFormat` / `HierarchyType` to follow the C# convention. Positional callers are unaffected; only named-argument callers must update.

**Before:** `new GetDimensionOrdersQuery(dimensionFormat: "Sachkontodimensionen", hierarchyType: DimensionHierarchyType.DataEntityLedgerDimensionFormat)`

**After:**

```csharp
Result<DimensionFormat> result = await mediator.Send(
    new GetDimensionOrdersQuery(
        DimensionFormat: "Sachkontodimensionen",
        HierarchyType: DimensionHierarchyType.DataEntityLedgerDimensionFormat),
    cancellationToken);

if (result.IsFailed && result.GetError() is IntegrationError { Type: ErrorType.NotFound })
{
    // The singleton DimensionParameters row is missing → NotFound, not an exception.
}
```

Rename the argument labels. See [Work with Dimensions](Work-with-Dimensions).

### `IODataClientAdapter.FindEntriesAsync` gained an `orderBy` parameter

An `orderBy` parameter (an `IReadOnlyList` of `(KeySelector, Descending)` tuples) was inserted before `skip` and `top`. This breaks anyone implementing `IODataClientAdapter` or calling `FindEntriesAsync` with named arguments for `skip` / `top`; most consumers reach queries through `IMediator` and are unaffected.

**Before:** `adapter.FindEntriesAsync<LedgerJournalHeader>(entitySet, filter, skip: 0, top: 50, cancellationToken)`

**After:**

```csharp
IEnumerable<LedgerJournalHeader> page = await adapter.FindEntriesAsync<LedgerJournalHeader>(
    "LedgerJournalHeaders",
    filter: h => h.DataAreaId == "USMF",
    orderBy: [(h => h.JournalBatchNumber, false)],
    skip: 0,
    top: 50,
    cancellationToken: cancellationToken);
```

Pass `orderBy: null` to keep the previous behaviour, or use named arguments for `skip` / `top` / `cancellationToken`. Prefer the typed `$orderby` overload on `IODataService<T>` in application code — see [Run Queries](Run-Queries).

## See Also
- [Send Commands](Send-Commands)
- [Known Limitations](Known-Limitations)
- [Define Entities](Define-Entities)
- [GitHub Releases](https://github.com/Mikeoso/IntegratoR/releases)
