# Release Notes and Versioning
> Last verified against v2.0.1

GitVersion computes every version in **ContinuousDelivery** mode: each merge to `main` produces a `X.Y.Z-ci.N` pre-release on NuGet, and a maintainer promotes a chosen build to a stable `X.Y.Z`. GitVersion defaults to a **PATCH** bump and ignores conventional-commit prefixes — signal a MINOR or MAJOR with a `+semver:` marker. Public API is deprecated for at least one MINOR before removal. The exhaustive log lives in [CHANGELOG.md](https://github.com/Mikeoso/IntegratoR/blob/main/CHANGELOG.md) and [GitHub Releases](https://github.com/Mikeoso/IntegratoR/releases); this page keeps a curated table and the per-MAJOR migration guide.

## How versions are produced

| Trigger | Version produced | NuGet listing |
|---|---|---|
| Push to `main` (every squash-merge) | `X.Y.Z-ci.N` pre-release | Pre-release, hidden by default |
| `gh workflow run publish.yml -f release=true` | `X.Y.Z` stable | Stable, shown by default |

Both publish to `nuget.org`. Pin an exact stable version (the latest stable is `2.0.0`); use a `2.0.*-*` range to track the latest pre-release. Tags are created by the publish workflow — never tag manually.

> [!CAUTION]
> GitVersion produces a PATCH bump on every merge (`2.0.1 → 2.0.2 → …`); `feat:` / `fix:` prefixes do **not** change this. To ship a MINOR or MAJOR, add `+semver: minor` or `+semver: major` to the merge commit message, or set `next-version:` in `GitVersion.yml` before merging.

## Pre-release vs stable

Consume a pre-release to test a fix against a sandbox before promotion:

```bash
dotnet add package IntegratoR.Hosting --version 2.0.1-ci.5
```

A `2.0.1-ci.5` build is immutable — the next push produces `2.0.1-ci.6`, never a silent update. Pin the stable version for production.

## Deprecate before remove

A public type is marked `[Obsolete("since vX.Y; use …")]` for at least one MINOR before it is removed in the next MAJOR. Awaiting the next MAJOR: `BaseEntity<TKey>`, `IODataService.FindAll` (use `FindAllAsync`), `ODataBatchException`, `ICacheableQuery.GenerateCacheKey` / `GetCacheKeyValues`, and `ODataMetadataProvider`. Derive from the non-generic `BaseEntity` now — see [Upgrading to v2](#upgrading-to-v2) below.

## Released Versions

| Version | Highlights |
|---|---|
| v2.0.1 (in flight, pre-release only) | Generic command validation fires through the pipeline; `UpdateAsync` returns the entity on a `204` composite-key PATCH; `LedgerJournalHeader` read-only fields marked `IgnoreOnUpdate`; null-safe `GetLoggingContext()`. |
| [v2.0.0](https://github.com/Mikeoso/IntegratoR/releases/tag/v2.0.0) | **Breaking.** Composite-key writes (Update/Delete/batch) live; typed `$orderby`; `ODataSettingsValidator`; `IBatchService<T>` + generic batch handlers; 401/403 + MSAL leak fixes. See [Upgrading to v2](#upgrading-to-v2). |
| [v1.3.5](https://github.com/Mikeoso/IntegratoR/releases/tag/v1.3.5) | FinancialDimension smoke test + dimension query fixes; enum qualified-type form in `Any`/`All` lambda bodies. |
| [v1.3.4](https://github.com/Mikeoso/IntegratoR/releases/tag/v1.3.4) | Cross-assembly handler closing; `BaseAddress` trailing-slash normalisation; `LedgerJournalLine` `CurrencyCode` payload fix. |
| [v1.3.3](https://github.com/Mikeoso/IntegratoR/releases/tag/v1.3.3) | `[JsonPropertyName]`-aware OData filter / select / expand translator — camelCase wire names honoured throughout the LINQ path. |
| [v1.2.0](https://github.com/Mikeoso/IntegratoR/releases/tag/v1.2.0) | **Breaking.** `ODataSettings` restructure — OAuth and resilience moved under `Authentication` / `Resilience` sub-objects. |
| [v1.1.0](https://github.com/Mikeoso/IntegratoR/releases/tag/v1.1.0) | Initial public release. |

For the full per-commit history, run `git log v2.0.0..HEAD --oneline` (or `git log v1.3.5..v2.0.0` for any two adjacent tags).

## Upgrading to v2

v2.0.0 carries three source-breaking API changes and one deprecation. Each has a mechanical fix; the migration is contained to code that calls these members directly. All other consumer code compiles unchanged.

### `BaseEntity<TKey>` → non-generic `BaseEntity`

The `TKey` parameter was never used. Derive from the non-generic `BaseEntity` and override `GetCompositeKey()` to return the key parts in composite-key order. `BaseEntity<TKey>` is `[Obsolete]` (removed next MAJOR).

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

Result result = await mediator.Send(
    new CreateBatchCommand<LedgerJournalHeader>(headers), cancellationToken);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    // D365 rejects a header whose JournalBatchNumber is set on create → IntegrationError, Type Failure.
}
```

Materialise the sequence with `.ToList()` (or `.ToArray()`) before constructing the command.

### `GetDimensionOrdersQuery` parameters are PascalCase

The positional record parameters were renamed `dimensionFormat` / `hierarchyType` → `DimensionFormat` / `HierarchyType` to follow the C# convention. Positional callers are unaffected; only named-argument callers must update.

**Before:** `new GetDimensionOrdersQuery(dimensionFormat: "Sachkontodimensionen", hierarchyType: DimensionHierarchyType.LedgerDimension)`

**After:**

```csharp
Result<DimensionFormat> result = await mediator.Send(
    new GetDimensionOrdersQuery(
        DimensionFormat: "Sachkontodimensionen",
        HierarchyType: DimensionHierarchyType.LedgerDimension),
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
- [Define Entities](Define-Entities)
- [Known Limitations](Known-Limitations)
- [Set Up Azure Functions Host](Set-Up-Azure-Functions-Host)
- [GitHub Releases](https://github.com/Mikeoso/IntegratoR/releases)
