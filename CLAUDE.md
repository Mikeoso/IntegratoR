# IntegratoR

.NET 10 integration framework using Clean Architecture, CQRS (MediatR), FluentResults, and Azure Functions (Durable Tasks). Bridges **D365 Finance & Operations** and **RELion** via OData.

## Project Structure

```
IntegratoR.Abstractions      — Domain interfaces, entities, CQRS contracts, Result types (innermost)
IntegratoR.Application        — Use cases, pipeline behaviours, cross-cutting concerns
IntegratoR.OData              — Generic OData client, HTTP, authentication, resilience
IntegratoR.OData.FO           — D365 Finance & Operations entity models and handlers
IntegratoR.RELion             — RELion OData integration and entity models
IntegratoR.SampleFunction     — Azure Functions host, composition root, orchestrators, activities
```

Dependencies point **inward only**. The Function host is the composition root.

## Key Architectural Decisions

1. **FluentResults over exceptions** — All operations return `Result<T>` or `Result`. Exceptions are reserved for truly unexpected failures.
2. **Generic CQRS handlers** — `CreateCommandHandler<TEntity>`, `GetByKeyQueryHandler<TEntity>` etc. work with any entity implementing `IEntity`.
3. **Composite keys via `GetCompositeKey()`** — D365 F&O entities often have multi-field keys. Every entity must implement `GetCompositeKey()`.
4. **Result serialization for Durable Functions** — Custom Newtonsoft.Json converters serialize `Result<T>` through orchestration replay.
5. **British spelling is intentional** — `Behaviour` not `Behavior` throughout the codebase. Never "correct" this.
6. **Dual JSON serializers** — System.Text.Json (`[JsonPropertyName]`) for entity models; Newtonsoft.Json (`[JsonProperty]`) for Durable Functions and RELion payloads. Do not unify them.

## Conventions

- **Branches:** `feature/<area>/<desc>`, `fix/<area>/<desc>`, `chore/<desc>`
- **Test naming:** `MethodName_Scenario_ExpectedResult`
- **Test stack:** xUnit v3, FluentAssertions 8.x, NSubstitute 5.x
- **Skills:** Use `microsoft-docs` for Azure/service concepts, `microsoft-code-reference` for SDK verification and code samples, `context7-docs` for third-party library docs (MediatR, Polly, FluentValidation, etc.)

## Canonical Examples

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

## Commands

```bash
dotnet build --no-restore             # Build
dotnet test                           # Run tests
dotnet format --no-restore            # Format code
dotnet list package --vulnerable      # Audit dependencies
```

## Versioning

**GitVersion** in `ContinuousDelivery` mode. Never manually edit `<Version>` in `.csproj` files.
