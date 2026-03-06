---
paths:
  - "**/*.cs"
---

# .NET Architecture & Patterns

## Pipeline Behaviour Registration Order (critical)

1. **LoggingBehaviour** — logs request start, outcome, timing
2. **ValidationBehaviour** — runs FluentValidation, short-circuits on failure
3. **CachingBehaviour** — serves/stores cached results for `ICacheableQuery`

Registered as open generics via `cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(...))`. Never reorder.

## Entity Design

- Inherit `BaseEntity<TKey>`, implement `GetCompositeKey()` (even single-key entities)
- `GetLoggingContext()` inherited from `BaseEntity` (reflection-based)
- Use `[Key]` on key properties, `required` on mandatory properties

## Feature File Organisation

```
Features/Commands/{Domain}/{OperationEntity}/
  {OperationEntity}Command.cs       # Single-entity (returns Result<TEntity>)
  {OperationEntity}Handler.cs
  {OperationEntity}sCommand.cs      # Batch (returns Result)
  {OperationEntity}sHandler.cs
```

One handler per file. Never combine multiple handlers.

## Durable Functions Constraints

- Orchestrators must be **deterministic**: no `DateTime.Now`, `Guid.NewGuid()`, direct I/O
- Activities return `Result<T>` or `Result` — never throw to the orchestrator
- Check `result.IsFailed` after every activity call — no try-catch for Result-returning activities
- Orchestration state limit ~4-5 MB — pass blob names for large data
