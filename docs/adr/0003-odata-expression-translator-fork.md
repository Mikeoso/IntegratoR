# ADR-0003: Forked OData expression translator for `[JsonPropertyName]`

- **Status:** Accepted
- **Date:** 2026-07-01

## Context

D365 F&O exposes ~479 legacy X++ system fields in **camelCase** (`dataAreaId`, `validFrom`, `recId`,
`itemId`, …) against ~19,604 PascalCase fields. IntegratoR declares those CLR properties in PascalCase
(C# convention) and maps them with `[JsonPropertyName("dataAreaId")]`. For strongly-typed LINQ to work
end-to-end, a filter like `x => x.DataAreaId == "USMF"` must emit `$filter=dataAreaId eq 'USMF'`.

`PanoramicData.OData.Client`'s expression parser reads `MemberInfo.Name` directly and **ignores
`[JsonPropertyName]`**, so it would emit the wrong wire name and D365 would reject the query.

## Decision

`IntegratoR.OData.Common.Filters.IntegratoRODataExpressionTranslator` is a **copy-and-patch** of
PanoramicData's expression parser (MIT — attribution in `THIRD_PARTY_LICENSES.md`) that resolves each
member's wire name through `[JsonPropertyName]` (via `PropertyNameResolver`) instead of
`MemberInfo.Name`. It has intentionally diverged in a few other places (D365 enum-literal handling,
.NET 10 targeting).

## Consequences

- Consumers never write raw OData filter strings; strongly-typed LINQ works for camelCase D365 fields.
- This is a **fork with a maintenance cost**: upstream fixes must be re-applied by hand. The divergences
  (e.g. the enum-comparison arm) are load-bearing — do not "clean them up" to match upstream.
- **Exit plan:** when the upstream PR adding `[JsonPropertyName]` support is merged and released, delete
  the local translator and switch back to the library. Track that as the trigger to retire this ADR.
