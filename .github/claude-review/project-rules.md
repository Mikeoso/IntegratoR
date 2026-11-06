# IntegratoR

> CI copy of the project instructions, read by the `claude-review` workflow. The local Claude Code
> setup (`CLAUDE.md`, `.claude/`) is not tracked; keep this folder in step with it.

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

Full language, style, and testing conventions (with examples) live in `.github/claude-review/csharp-coding-standards.md`.

