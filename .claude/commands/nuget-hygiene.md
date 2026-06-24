Audit NuGet package hygiene for IntegratoR.

$ARGUMENTS

If $ARGUMENTS is empty, audit the whole solution. If it names a package, scope the report to that package.

---

## Steps

1. From the repo root, run:
   - `dotnet list IntegratoR.sln package --deprecated`
   - `dotnet list IntegratoR.sln package --outdated`

   Do **not** re-run `--vulnerable` — CI already hard-gates it at `.github/workflows/build.yml`. Note that in the report (and the `vuln-check` PostToolUse hook flags it locally on `Directory.Packages.props` edits).
2. Emit one table: `package | current | latest | deprecated? | suggested action`.
3. For each proposed bump, ask only: does it change anything on the **published `IntegratoR.*` surface** (a transitive dependency exposed by a packable assembly, or a behaviour change consumers see)? If yes, classify the impact and decide whether a version bump is warranted by following `.claude/rules/api-compatibility.md` + the `csharp-api-design` skill. Do **not** restate those rules here — defer to them.

## Constraints

- **Propose only** — never auto-bump a version.
- Edit versions **only** in `Directory.Packages.props` (Central Package Management); never add a `Version=` attribute to a `.csproj`.
- Branch / commit / PR per `.claude/rules/common.md` Git Workflow + the CLAUDE.md Default Workflow; commit only when asked; never tag manually (GitVersion + `publish.yml` own releases).
- This is an on-demand audit, not a package-management convention — do not duplicate CPM rules (owned by CLAUDE.md + the `csharp-coding-standards` skill).
