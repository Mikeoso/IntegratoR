# API Compatibility

The `IntegratoR.*` library projects (everything except the `SampleFunction` host) pack and publish to NuGet on every push to `main`. Their public surface is a contract. The `csharp-api-design` skill holds the generic how-to; this file holds only the IntegratoR-specific exceptions.

## Public surface is a contract

- Public types/members in the library projects are downstream API. Removing, renaming, narrowing visibility, changing a signature, or adding a required parameter is breaking — keep changes **additive**.
- Adding an optional parameter to an **already-shipped** public method is a *binary* break; add a new overload instead. (New methods with optional params are fine.)
- The **"no defensive code" rule does NOT apply to public library API.** A lenient fallback that looks dead inside this repo may be load-bearing for an external consumer. PR #84 tightened the public `ResultJsonShape.Project` fallback and shipped an unannounced break — reverted in review. Do not tighten accepted public inputs or change public serialised output without an explicit breaking-change plan.
- `Result<T>` rides two serialisers — `architecture.md` owns that wire contract; keep both converters in lockstep (do not restate the detail here).

## Do not seal by default

IntegratoR is extension-by-design. Keep these **open**:

- `BaseEntity<TKey>` and the F&O entities that inherit it.
- Generic commands / behaviours / validators and the `IService<T>` / `IODataService<T>` interfaces consumers implement or extend.
- DI-registered service implementations a consumer may replace.

Seal only leaf infrastructure with no inheritance story (as already done for `IntegratoRBuilder`, `ODataFieldAttribute`, and the `Result<T>` converters).

## Versioning maps to GitVersion

- Versioning is GitVersion-driven (ContinuousDelivery); it defaults to **PATCH** and ignores conventional-commit prefixes.
- A public removal/rename/visibility-narrowing/signature change is **MAJOR**; additive API is **MINOR** — signal with a `+semver: minor|major` commit marker or `next-version` in `GitVersion.yml`. Never tag manually; the publish workflow tags.
- Deprecate before removing: `[Obsolete("since vX.Y; use …")]` for at least one MINOR, remove in the next MAJOR.
