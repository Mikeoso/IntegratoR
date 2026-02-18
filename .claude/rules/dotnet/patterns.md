---
paths:
  - "**/*.cs"
---

# .NET Architecture & Patterns

> This file extends [common rules](../common/) with the specific architectural patterns used in this solution.

## Clean Architecture Layers

```
IntegratoR.Abstractions      (innermost — domain interfaces, entities, CQRS contracts)
    ^
IntegratoR.Application        (use cases — behaviours, cross-cutting concerns)
    ^
IntegratoR.OData              (infrastructure — OData client, HTTP, authentication)
IntegratoR.OData.FO           (infrastructure — D365 Finance & Operations specifics)
IntegratoR.RELion             (infrastructure — RELion-specific integration)
    ^
IntegratoR.SampleFunction     (entry point — Azure Functions host, composition root)
```

Dependencies point **inward only**. Infrastructure projects reference Abstractions and Application. The Function host references everything and composes the DI container.

## CQRS with MediatR

Commands and queries are defined as **record types** implementing marker interfaces from `IntegratoR.Abstractions`:

```csharp
// Command — mutates state, returns Result or Result<T>
public record CreateCommand<TEntity>(TEntity Entity) : ICommand<Result<TEntity>>
    where TEntity : IEntity;

// Query — reads state, returns Result<T>
public record GetByKeyQuery<TEntity>(object[] KeyValues) : IQuery<Result<TEntity>>
    where TEntity : IEntity;

// Cacheable query — adds cache metadata
public record GetDimensionOrdersQuery(string dimensionFormat, HierarchyType hierarchyType)
    : ICacheableQuery<Result<IEnumerable<DimensionOrder>>>
{
    public string CacheKey => GenerateCacheKey();
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(30);
    public object[] GetCacheKeyValues() => [dimensionFormat, hierarchyType];
    public string GenerateCacheKey() => $"DimensionOrders-{dimensionFormat}-{hierarchyType}";
}
```

All commands and queries implement `IContext` for structured logging via `GetLoggingContext()`.

## Pipeline Behaviour Registration Order

**This order is critical** — register behaviours in the MediatR pipeline in this sequence:

1. **LoggingBehaviour** — logs request start, outcome, and timing
2. **ValidationBehaviour** — runs FluentValidation, short-circuits on failure
3. **CachingBehaviour** — serves cached results for `ICacheableQuery`, caches successful responses only

Each behaviour is an `IPipelineBehavior<TRequest, TResponse>` registered as open generics:

```csharp
services.AddMediatR(cfg =>
{
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CachingBehaviour<,>));
});
```

## Dependency Injection Composition

Each project exposes **one** static class named `ApplicationDependencyInjection` with public extension methods on `IServiceCollection`:

```csharp
// In IntegratoR.OData/Common/Extensions/ApplicationDependencyInjection.cs
public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddODataClient(
        this IServiceCollection services,
        IConfiguration configuration) { ... }
}
```

- One class per project, in the `Common/Extensions` folder
- One or more `Add{Feature}` methods as the public API
- All internal registrations are private helper methods
- The Azure Function host calls these methods to compose the full container

## Entity Design

Entities inherit from `BaseEntity<TKey>` and implement:
- `GetCompositeKey()` — returns the primary key components (many D365 entities have composite keys)
- `GetLoggingContext()` — inherited from `BaseEntity`, uses reflection to capture all public properties

```csharp
public class SalesOrder : BaseEntity<string>
{
    [Key] public required string SalesOrderNumber { get; init; }
    [Key] public required string DataAreaId { get; init; }

    public override object[] GetCompositeKey() => [SalesOrderNumber, DataAreaId];
}
```

Use `[Key]` attributes for key properties. Use `required` for mandatory properties.

## Configuration via Options Pattern

Bind configuration sections to strongly-typed settings classes using `IOptions<T>`:

```csharp
services.Configure<ODataSettings>(configuration.GetSection("ODataSettings"));
```

Settings classes live in the `Domain/Settings` folder of their respective project. Provide sensible defaults for optional settings (retry counts, timeouts, feature flags).

## Feature File Organisation

Commands and queries follow a strict folder structure within each infrastructure project:

```
Features/
  Commands/
    {Domain}/
      {OperationEntity}/
        {OperationEntity}Command.cs          # Single-entity command (record)
        {OperationEntity}Handler.cs          # Single-entity handler
        {OperationEntity}sCommand.cs         # Batch command (plural, record)
        {OperationEntity}sHandler.cs         # Batch handler
  Queries/
    {Domain}/
      {QueryName}/
        {QueryName}Query.cs
        {QueryName}QueryHandler.cs
```

Single commands return `Result<TEntity>`. Batch commands return non-generic `Result`. Each handler has its own file — never combine multiple handlers in one file.

Example: `Features/Commands/LedgerJournals/CreateLedgerJournalHeader/` contains `CreateLedgerJournalHeaderCommand.cs`, `CreateLedgerJournalHeaderHandler.cs`, `CreateLedgerJournalHeadersCommand.cs`, and `CreateLedgerJournalHeadersHandler.cs`.

## Durable Functions Patterns

### Orchestrator Constraints

Orchestrators must be **deterministic** — they replay from the beginning on every wake-up. Never use:
- `DateTime.Now` or `DateTime.UtcNow` (use `context.CurrentUtcDateTime`)
- `Guid.NewGuid()` (use `context.NewGuid()`)
- Direct I/O, HTTP calls, or Thread.Sleep (delegate to activities)
- Non-deterministic conditionals that change between replays

### Activity Functions

Activities perform the actual work. Follow these conventions:
- Return `Result<T>` or `Result` — never throw exceptions to the orchestrator
- Wrap all external calls in try-catch, converting exceptions to `IntegrationError`
- Use primary constructors for dependency injection
- Async activities return `Task<Result<T>>`; synchronous return `Result<T>` directly
- Name activities with a descriptive verb suffix: `ReadBlobActivity`, `MapLinesActivity`, `CreateJournalLinesActivity`

### Orchestrator Error Handling

Orchestrators check `result.IsFailed` after every activity call — never use try-catch for Result-returning activities:

```csharp
var result = await context.CallActivityAsync<Result<T>>(nameof(MyActivity), input);

if (result.IsFailed)
{
    logger.LogError("Failed: {Error}", result.GetError()?.Message);
    return Result.Fail(result.Errors);
}
```

### Fan-Out/Fan-In with Sub-Orchestrations

Group work items, start sub-orchestrations in parallel, then aggregate:

```csharp
var tasks = groups.Select(g =>
    context.CallSubOrchestratorAsync<Result>(nameof(SubOrchestrator), g));
await Task.WhenAll(tasks);
```

### Serialization Limits

Durable Functions orchestration state has practical size limits (~4-5 MB). For large data (file contents, bulk entity lists), use blob storage and pass the blob name through orchestration state instead.

**Reference:** `IntegratoR.SampleFunction/Orchestrators/JournalOrchestrators.cs`

## Anti-Patterns

Things to **never do** in this codebase:

### Result Pattern Violations
- **Never throw exceptions for business logic** — use `Result.Fail()` with `IntegrationError`. Exceptions are for truly unexpected failures only.
- **Never use try-catch in orchestrators** for Result-returning activities. Check `result.IsFailed` instead.
- **Never catch exceptions in pipeline behaviours** without re-throwing. Behaviours log and re-throw; they don't swallow.

### Architecture Violations
- **Never create additional DI registration classes** — each project has exactly one `ApplicationDependencyInjection` static class in `Common/Extensions/`.
- **Never unify Newtonsoft.Json and System.Text.Json** — they serve different purposes and must coexist.
- **Never reorder pipeline behaviours** — the registration order (Logging -> Validation -> Caching) is critical and intentional.

### Entity Violations
- **Never skip `GetCompositeKey()` on entities** — all entities must implement it, even single-key entities. Generic handlers depend on it.
- **Never use `DateTime.Now` in orchestrators** — use `context.CurrentUtcDateTime` for deterministic replay.

### Build & Versioning
- **Never edit `<Version>` in `.csproj` files** — GitVersion computes versions from git history.
- **Never change `<TargetFramework>`, `<LangVersion>`, `<Nullable>`, or `<ImplicitUsings>`** project settings.
