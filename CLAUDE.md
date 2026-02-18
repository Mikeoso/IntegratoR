# IntegratoR

.NET 10 integration framework using Clean Architecture, CQRS (MediatR), FluentResults, and Azure Functions (Durable Tasks). Bridges **D365 Finance & Operations** and **RELion** via OData, orchestrating financial journal data flows between the two systems.

## Tech Stack

| Package | Version | Purpose |
|---|---|---|
| MediatR | 12.5.0 | CQRS command/query dispatch |
| FluentResults | 4.0.0 | Result pattern (replaces exceptions for flow control) |
| FluentValidation | 12.0.0 | Request validation in pipeline |
| MSAL (Microsoft.Identity.Client) | 4.76.0 | OAuth2 client credentials for D365/RELion |
| Simple.OData.Client | 6.0.1 | OData v4 entity operations |
| Polly | 8.5.0 | Retry + circuit breaker resilience |
| Azure Functions Worker | 2.0.0 | Isolated-process Azure Functions host |
| Durable Task Extensions | 1.7.1 | Orchestration, activities, fan-out/fan-in |
| Newtonsoft.Json | 13.0.3 | Durable Functions serialization (required) |
| StackExchange.Redis | 2.8.16 | Distributed caching |

## Project Structure

```
IntegratoR.Abstractions      — Domain interfaces, entities, CQRS contracts, Result types (innermost)
IntegratoR.Application        — Use cases, pipeline behaviours, cross-cutting concerns
IntegratoR.OData              — Generic OData client, HTTP, authentication, resilience
IntegratoR.OData.FO           — D365 Finance & Operations entity models and handlers
IntegratoR.RELion             — RELion OData integration and entity models
IntegratoR.SampleFunction     — Azure Functions host, composition root, orchestrators, activities
```

Dependencies point **inward only**. The Function host is the composition root that wires everything together.

## Key Architectural Decisions

1. **FluentResults over exceptions** — All operations return `Result<T>` or `Result`. Exceptions are reserved for truly unexpected failures. This enables Durable Functions orchestrators to inspect outcomes without try-catch.
2. **Generic CQRS handlers** — `CreateCommandHandler<TEntity>`, `GetByKeyQueryHandler<TEntity>` etc. work with any entity implementing `IEntity`, reducing boilerplate across D365 entity types.
3. **Composite keys via `GetCompositeKey()`** — D365 F&O entities often have multi-field keys (DataAreaId + JournalBatchNumber). Every entity must implement `GetCompositeKey()` to enable generic key-based operations.
4. **Result serialization for Durable Functions** — Custom Newtonsoft.Json converters (`ResultJsonConverter`) serialize `Result<T>` through orchestration replay. Configured globally in `Program.cs` via `JsonConvert.DefaultSettings`.
5. **British spelling is intentional** — `Behaviour` not `Behavior` throughout the codebase. Never "correct" this.
6. **Dual JSON serializers** — System.Text.Json (`[JsonPropertyName]`) for entity models and standard .NET; Newtonsoft.Json (`[JsonProperty]`) for Durable Functions serialization and RELion response payloads. Do not attempt to unify them.

## Rules

Project conventions are defined in `.claude/rules/`:

- **`common/`** — Language-agnostic rules: git workflow, performance, security, testing
- **`dotnet/`** — .NET-specific rules: coding style, patterns, testing, hooks, security (path-scoped to `*.cs`, `*.csproj`, `*.json`)

## Key Commands

```bash
dotnet build                          # Build the solution
dotnet build --no-restore             # Build without restoring packages
dotnet format --no-restore            # Format code (default rules, no .editorconfig yet)
dotnet test                           # Run tests (no test projects yet — conventions in rules)
dotnet list package --vulnerable      # Check for vulnerable dependencies
func start                           # Run Azure Functions locally (from SampleFunction dir)
```

## Canonical Examples

These files are reference implementations for each pattern:

| Pattern | Reference File |
|---|---|
| Entity with composite key | `IntegratoR.OData.FO/Domain/Entities/LedgerJournal/LedgerJournalHeader.cs` |
| Single CQRS command + handler | `IntegratoR.OData.FO/Features/Commands/LedgerJournals/CreateLedgerJournalHeader/` |
| Batch CQRS command + handler | `IntegratoR.OData.FO/Features/Commands/LedgerJournals/CreateLedgerJournalLine/CreateLedgerJournalLinesHandler.cs` |
| Pipeline behaviour | `IntegratoR.Application/Common/Behaviours/LoggingBehaviour.cs` |
| DI composition | `IntegratoR.OData/Common/Extensions/ApplicationDependencyInjection.cs` |
| Orchestrator (fan-out/fan-in) | `IntegratoR.SampleFunction/Orchestrators/JournalOrchestrators.cs` |
| Activity functions | `IntegratoR.SampleFunction/Functions/JournalActivities.cs` |
| Result serialization setup | `IntegratoR.SampleFunction/Program.cs` |
| IntegrationError usage | `IntegratoR.Abstractions/Common/Results/IntegrationError.cs` |

## Versioning

This project uses **GitVersion** in `ContinuousDelivery` mode. Never manually edit `<Version>` in `.csproj` files — versions are computed from git history. See `.claude/rules/common/git-workflow.md` for branch naming conventions.

## Personal Preferences

Create a `CLAUDE.local.md` in the project root for personal preferences (editor settings, preferred patterns, etc.). This file is gitignored and will be loaded automatically by Claude Code alongside this file.
