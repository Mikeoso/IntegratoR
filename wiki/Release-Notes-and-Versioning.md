# Release Notes and Versioning

IntegratoR packages follow [Semantic Versioning](https://semver.org/) — `MAJOR.MINOR.PATCH`. Versions are computed by GitVersion in **ContinuousDelivery** mode on every merge to `main` and are pinned by Git tags created automatically by the publish workflow.

## Version Model

| Trigger | Version produced | NuGet listing |
|---|---|---|
| Push to `main` (every PR squash-merge) | `1.X.Y-ci.N` pre-release | Pre-release, hidden by default in NuGet UI |
| Manual workflow dispatch with `release: true` | `1.X.Y` stable | Stable, shown by default |

Both publish to `nuget.org`. Consumers pinning `1.3.5` pick up the stable version; consumers using a `*-*` range will see the latest pre-release. Choose the pin shape according to risk tolerance.

> **GitVersion defaults to PATCH bumps.** Conventional-commit prefixes (`feat:`, `fix:`, `chore:`) are **not** honoured for bump detection in the current `GitVersion.yml` configuration. Every merge produces a PATCH bump (1.3.4 → 1.3.5 → 1.3.6, ...). To force a MINOR or MAJOR bump, either include `+semver: minor` / `+semver: major` in the merge commit message, or bump `next-version:` in `GitVersion.yml` before merging.

## Released Versions

| Version | Date | Highlights |
|---|---|---|
| v1.3.5 | 2026-05-13 | FinancialDimension smoke test + dimension query fixes (PR #104). Enum-constant qualified-type form in lambda bodies. `DimensionParameters.Key` `string → int`. `DimensionIntegrationFormat` table-name plural fix. |
| v1.3.4 | 2026-04-15 | Smoke-test framework fixes (PR #92): MediatR cross-assembly handler closing, BaseAddress trailing-slash normalisation, ExceptionHandler 404 observability, `CurrencyCode` payload fix on `LedgerJournalLine`. |
| v1.3.3 | (in series with PR #86) | `[JsonPropertyName]`-aware OData filter / select / expand translator (PR #86). camelCase wire names now honoured throughout the LINQ path. |
| v1.3.0 | — | (PR #79 / OData BaseAddress bug fix). |
| v1.2.0 | (PR #76) | **Breaking** — ODataSettings restructure. `ClientId`/`ClientSecret`/`TenantId`/`Resource` moved from `ODataSettings` root to `ODataSettings.Authentication.OAuth.*`. Retry/circuit-breaker fields moved to `ODataSettings.Resilience.*`. |
| v1.1.0 | — | Initial public release. |

Each row links a release version to the major PRs that landed in it. For the full per-commit history use `git log v1.3.4..v1.3.5 --oneline` (or the equivalent for any two adjacent tags).

## Migration Guides

### Upgrading from v1.1.x to v1.2.0 (or later)

The `ODataSettings` structure changed from flat to nested. The OAuth and resilience properties moved under typed sub-objects.

**Before (v1.1.x):**

```json
{
  "ODataSettings": {
    "Url": "...",
    "ClientId": "...",
    "ClientSecret": "...",
    "TenantId": "...",
    "Resource": "...",
    "EnableRetries": true,
    "RetryCount": 3,
    "UseCircuitBreaker": true,
    "CircuitBreakerThreshold": 5,
    "CircuitBreakerDurationSeconds": 30
  }
}
```

**After (v1.2.0+):**

```json
{
  "ODataSettings": {
    "Url": "...",
    "Authentication": {
      "Mode": "OAuth",
      "OAuth": {
        "ClientId": "...",
        "ClientSecret": "...",
        "TenantId": "...",
        "Resource": "..."
      }
    },
    "Resilience": {
      "EnableRetries": true,
      "RetryCount": 3,
      "UseCircuitBreaker": true,
      "CircuitBreakerThreshold": 5,
      "CircuitBreakerDurationInSeconds": 30
    }
  }
}
```

Two additional gotchas:

- The `Mode` selector defaults to `ApiKey`. OAuth-based setups must add `"Mode": "OAuth"` explicitly.
- The circuit breaker duration property was renamed `CircuitBreakerDurationSeconds → CircuitBreakerDurationInSeconds`. The old key is silently ignored under typed binding.

Azure App Settings update accordingly — the nested keys use the double-underscore separator (`ODataSettings__Authentication__OAuth__ClientId`, ...).

### Upgrading from v1.3.4 to v1.3.5

No breaking changes. Two consumer-visible improvements:

- LINQ filter expressions using enum constants inside `Any` / `All` lambda bodies now emit the D365-compatible qualified-type form. If a consumer was working around the prior behaviour by using captured variables or raw filter strings, those workarounds can be removed.
- `GetDimensionOrdersQuery` now returns `Result.Fail(IntegrationError("DimensionParameters.NotFound", ..., NotFound))` when the singleton parameters row is missing instead of throwing `ArgumentOutOfRangeException`. Consumers that already handled `Result.IsFailed` work unchanged.

The dimension-related entity changes (`DimensionIntegrationFormat` plural table, `DimensionParameters.Key` `int`) are runtime-only — no API surface change for consumers using only the typed query.

## Release Process

For maintainers — full mechanics are in `feedback-gitversion-tagging` and `deployment-workflow` internal memory files. Summary:

1. Merge the PR to `main` via the GitHub UI (squash-merge).
2. The push triggers `publish.yml` automatically — pre-release `1.X.Y-ci.N` ships to NuGet within ~3 minutes.
3. When ready to cut a stable release: `gh workflow run publish.yml -f release=true --ref main`.
4. The release job:
   - Runs GitVersion to compute `MajorMinorPatch` (`1.X.Y`).
   - Packs and pushes stable packages.
   - Creates a Git tag `vX.Y.Z` via `softprops/action-gh-release@v2`.
   - Creates a GitHub Release with auto-generated notes from the merged PR titles since the previous tag.

Tags are **never** created manually — the workflow is authoritative.

## Pre-release Channel

Pre-release versions are useful for:

- Testing a fix against a sandbox before the stable release ships.
- Pinning a known-good build that has not yet been promoted.

To consume a pre-release:

```bash
dotnet add package IntegratoR.Hosting --version 1.3.6-ci.5
```

Or in `Directory.Packages.props`:

```xml
<PackageVersion Include="IntegratoR.Hosting" Version="1.3.6-ci.*" />
```

Pre-release versions are immutable once published. A `1.3.6-ci.5` build is not retroactively updated — the next push produces `1.3.6-ci.6`.

## Where to Find What

- **Package downloads** — `https://www.nuget.org/packages?q=IntegratoR`
- **Per-version release notes** — `https://github.com/Mikeoso/IntegratoR/releases`
- **Per-commit history** — `git log` on the repository
- **Roadmap / known limitations** — [Known Limitations](Known-Limitations)
- **Per-PR review and discussion** — `https://github.com/Mikeoso/IntegratoR/pulls?q=is%3Apr+is%3Aclosed`

## See Also

- [Configure OData](Configure-OData) — current settings reference (post-v1.2.0 nested shape)
- [Known Limitations](Known-Limitations) — what's parked for future releases
- [Set Up Azure Functions Host](Set-Up-Azure-Functions-Host) — production deployment pattern that consumes these packages
- [Source on GitHub](https://github.com/Mikeoso/IntegratoR) — full commit history and source code
