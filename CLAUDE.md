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
IntegratoR.RELion/          → RELion API integration (auth, service, entities, queries)
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

## Output Format

- Short sections, small code blocks, explain trade-offs.
- When making changes: show diff-level guidance + why.
- Lead with the answer, not the reasoning.
