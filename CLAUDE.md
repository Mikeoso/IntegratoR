# IntegratoR

.NET 10 framework for building D365 Finance & Operations integrations via OData on Azure Functions. Clean Architecture, CQRS with MediatR, FluentResults, FluentValidation.

## Tech Stack

.NET 10 / C# preview, Azure Functions (isolated worker), MediatR, FluentResults, FluentValidation, Polly, PanoramicData.OData.Client, GitVersion, xUnit v3, FluentAssertions, NSubstitute.

## Repo Map

```
IntegratoR.Abstractions/  → domain interfaces, base entities, CQRS contracts, Result pattern
IntegratoR.Application/   → MediatR pipeline behaviours, generic handlers, OAuthAuthenticator, cache
IntegratoR.OData/          → generic OData client, auth handler, Polly policies, ODataFieldAttribute
IntegratoR.OData.FO/       → D365 F&O entities, dimension queries, feature-specific handlers
IntegratoR.Hosting/         → IntegratoRBuilder, composition root helpers
IntegratoR.SampleFunction/ → Azure Functions host (composition root)
tests/IntegratoR.TestKit/  → shared test infrastructure (fakes, Result assertions, builders)
tests/*/                    → test projects mirroring source structure
wiki/                       → GitHub wiki documentation source
```

## Hard Rules

- **FluentResults `Result<T>`** for all returns — NEVER throw exceptions for business flow.
- **`ConfigureAwait(false)`** in all library code (everything except SampleFunction host).
- **`CancellationToken`** propagated through all async method chains.
- **British spelling**: `Behaviour` not `Behavior` (intentional throughout).
- **Central Package Management**: versions in `Directory.Packages.props` only — no version attributes in csproj.
- **No AutoMapper** — write explicit mappings.
- **No repository pattern** wrapping the service layer.
- **No `var` for non-obvious types** — prefer explicit types when the type is not apparent.
- **No sync-over-async** — no `.Result`, `.Wait()`, `Task.Run` in library code.
- **Doc comments describe actual behaviour** — never ship stale/aspirational docs; lean by default (concise `<summary>`, `<remarks>`/inline `//` only when a WHY is non-obvious); no `FILE-LEVEL` banner comments; `Result<T>` failures documented in `<returns>`/`<remarks>`, not `<exception>`.

Full language, style, and testing conventions (with examples) live in the `csharp-coding-standards` skill; documentation conventions live in the `csharp-documentation` skill.

## Default Workflow

1. **Interview** — for non-trivial tasks, ask focused questions with options before starting.
2. **Plan** — propose approach + list files to touch. Get approval.
3. **Implement** — smallest change that works. No scope creep.
4. **Test** — run `dotnet build` + `dotnet test`. Prove it works.
5. **Review** — delegate to `code-reviewer` (always) and `security-reviewer` (for auth code).
6. **Commit** — atomic commits grouped logically. Imperative mood, under 72 chars.

## Commands

```bash
dotnet build                                    # Build entire solution
dotnet test                                     # Run all tests
dotnet test --filter "FullyQualifiedName~Class.Method"  # Run single test
dotnet test tests/IntegratoR.OData.Tests        # Run one project's tests
dotnet format                                   # Format code
dotnet format --verify-no-changes               # Check formatting (CI)
```

## Skills & Agents

Route work before starting (this replaces the former `.claude/rules/skill-routing.md`):

| When | Use |
|------|-----|
| Non-trivial feature, restructuring, or design touching architecture | Plan mode + interview — focused questions with options, get approval before implementing |
| Writing, reviewing, or refactoring C# (handlers, services, entities, commands/queries, tests) | `csharp-coding-standards` skill |
| Writing, reviewing, or trimming C# doc comments (`///` XML docs) or inline `//` comments | `csharp-documentation` skill |
| Writing, editing, restructuring, or deploying **wiki** pages (`/wiki` → `.wiki.git`) — conceptual/how-to/reference prose | `wiki-documentation` skill |
| Changing the public/published API surface of a library (signatures, visibility, serialised output, `[Obsolete]`) | `csharp-api-design` skill |
| Write / add / plan / review tests | `test-planning` skill (auto-triggers) → briefs the `test-writer` agent. Skip trivial structural tests (DI registration, config binding, POCO properties) |
| Review code changes — correctness, quality, architecture, test coverage | `code-reviewer` agent |
| Review auth, secrets, or HTTP-header changes | `security-reviewer` agent |
| Third-party library API/usage (Polly, MediatR, FluentValidation, FluentResults, PanoramicData.OData.Client, xUnit, NSubstitute) | `context7-docs` skill |
| Microsoft Learn docs — .NET / Azure concepts, limits, config | `microsoft-docs` skill |
| Microsoft SDK signatures or code samples | `microsoft-code-reference` skill |
| Generate or update the IntegratoR wiki | `/docs` (alias `/documentation`) command — drives the `wiki-documentation` skill |
| Draft a PR description (summary, risks, rollback, reviewer focus) for the branch | `/dotnet-pr` command |
| Audit package hygiene — deprecated / outdated dependencies before a bump | `/nuget-hygiene` command |
| Consolidate session learnings into memory | `/dream` skill |

Bug fixes, single-file changes, and renames need no routing — just do them.

## Output Format

- Short sections, small code blocks, explain trade-offs.
- When making changes: show diff-level guidance + why.
- Lead with the answer, not the reasoning.
