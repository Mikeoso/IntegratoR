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
