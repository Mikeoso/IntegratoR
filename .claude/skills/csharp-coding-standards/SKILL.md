---
name: csharp-coding-standards
description: >-
  Use when writing, reviewing, or refactoring C# in this repository — authoring a
  handler/service/entity/command/query, adding a test, or fixing a .cs file.
  Enforces IntegratoR's FluentResults Result<T> error model (never throw for
  business flow), async discipline (ConfigureAwait(false), CancellationToken),
  records-for-CQRS vs mutable-OData-entities, naming, file-scoped namespaces,
  British spelling (Behaviour), Central Package Management, and xUnit v3 +
  NSubstitute + FluentAssertions + TestKit assertions. Do NOT use for docs/wiki,
  JSON/config, PowerShell, or non-C# changes.
---

# IntegratoR C# Coding Standards

> C#-specific conventions for the IntegratoR framework (.NET 10 / C# 14, `LangVersion=preview`). Pairs with the project rules in `.claude/rules/` — this skill owns the *language and style* conventions; the rules files own architecture, domain (OData/resilience), workflow, and security.

## Scope

**Applies to**: `.cs` files, `.csproj` projects, C# code blocks in this repo.
**Does NOT apply to**: F#, VB.NET, PowerShell, non-.NET code.
**Defers to**: `.claude/rules/architecture.md` (Clean Architecture, CQRS/MediatR, serialisation), `.claude/rules/odata-conventions.md` (entities, settings), `.claude/rules/perf-reliability.md` (HTTP/resilience), `.claude/rules/security.md`, and `.claude/rules/api-compatibility.md` / the `csharp-api-design` skill (public library surface).

## Core Principles

1. **Result over exceptions** — return `FluentResults.Result<T>`; NEVER throw for business flow. Exceptions are reserved for truly exceptional cases (network failure, null ref) and are converted to `Result<T>` at the boundary.
2. **Nullable enabled everywhere** — NRT is on solution-wide (`Directory.Build.props`). Annotate all reference types.
3. **Immutability where it fits** — CQRS commands/queries and HTTP DTOs are immutable `record`s. OData-bound entities are deliberately MUTABLE classes (see below).
4. **Prefer modern language features** — pattern matching, collection expressions `[...]`, `required` members, file-scoped namespaces.
5. **Explicit over implicit** — no `var` for non-obvious types; explicit mappings (no AutoMapper).
6. **British spelling for identifiers** — `Behaviour`, not `Behavior` (intentional throughout; e.g. `LoggingBehaviour`, `ValidationBehaviour`, `CachingBehaviour`).

> **Public library API exception:** for the published `IntegratoR.*` surface, the "no defensive code" / "only validate at boundaries" guidance below is INVERTED — a lenient fallback that looks dead in-repo may be load-bearing for an external consumer. See `api-compatibility.md` / the `csharp-api-design` skill before tightening accepted public inputs.

## Patterns

### Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Types (classes, records, enums) | PascalCase | `ODataService`, `ErrorType` |
| Interfaces | `I` + PascalCase | `IService<T>`, `IODataBatchService<T>` |
| Methods | PascalCase | `AddAsync`, `GetCompositeKey` |
| Async methods | PascalCase + `Async` | `AddAsync`, `FindAllAsync` |
| Properties | PascalCase | `DataAreaId`, `JournalName` |
| Private fields | `_camelCase` | `_service`, `_logger`, `_client` |
| Parameters & locals | camelCase | `cancellationToken`, `entitySetName` |
| Generic type params | `T` + PascalCase | `TEntity`, `TResponse`, `TKey` |
| Pipeline behaviours | British `Behaviour` | `LoggingBehaviour`, `CachingBehaviour` |
| Generic commands | `XxxCommand<T>` | `CreateCommand<T>`, `UpdateCommand<T>`, `DeleteCommand<T>` |
| Generic queries | `GetXxxQuery<T>` | `GetByKeyQuery<T>`, `GetByFilterQuery<T>` |

> **Known exception:** some pinned positional records use camelCase parameters by design (e.g. `GetDimensionOrdersQuery(string dimensionFormat, DimensionHierarchyType hierarchyType)`). This signature is deliberate — do not "fix" it to PascalCase.

### Error Handling — `Result<T>` + `IntegrationError`, never throw for flow

This is the most important deviation from generic C# guidance. **Do not throw exceptions for business or validation flow.** Model expected failures as `IntegrationError` (a `FluentResults.Error` subclass carrying a machine-readable `Code` and an `ErrorType`) wrapped in `Result<T>`.

```csharp
// Guard at a boundary by RETURNING a failed Result — do NOT throw.
public async Task<Result<TEntity>> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
{
    if (entity is null)
        return Result.Fail<TEntity>(
            new IntegrationError("Entity.Null", "Entity must not be null.", ErrorType.Validation));

    return await _client.CreateAsync(_entitySetName, entity, cancellationToken).ConfigureAwait(false);
}

// ErrorType maps to HTTP status / log level: Failure, Validation, NotFound, Conflict.
return Result.Fail<Order>(
    new IntegrationError("Order.Empty", "Order must have at least one item.", ErrorType.Validation));

// Projecting a Result<T> with FluentResults' .Match (idiomatic across the FO handlers):
return addResult.Match(
    onSuccess: entity => Result.Ok(entity),
    onFailure: errors => Result.Fail<TEntity>(errors));
```

The ONE legitimate `throw` site: low-level exceptions (e.g. `ODataNotFoundException`) thrown *inside* an exception-handler wrapper whose job is to catch them and project them to `Result<T>`. They never escape as control flow.

```csharp
// Inside _exceptionHandler.ExecuteAsync(...) — caught and mapped to Result<T> at the boundary.
throw new ODataNotFoundException(key);
```

**Avoid:**
```csharp
// Throwing for business flow — forbidden here.
throw new NotFoundException($"User {id} not found");

// Guard-by-throw at a public boundary — return Result.Fail instead.
ArgumentNullException.ThrowIfNull(entity);

// Catching or throwing base Exception, or empty catch blocks.
catch (Exception ex) { /* swallow */ }
throw new Exception("Something went wrong");
```

Assert failures in tests with `result.Should().BeFailed()` / `HaveErrorCode(...)` / `HaveErrorType(...)` — there is no exception to catch.

### Records vs Mutable Entities

**CQRS commands, queries, and HTTP DTOs are immutable records.**

```csharp
// Generic CQRS commands/queries — positional records.
public record CreateCommand<TEntity>(TEntity Entity) : ICommand<Result<TEntity>>
    where TEntity : IEntity;

public record GetByKeyQuery<TEntity>(object[] CompositeKey) : IQuery<Result<TEntity>>;

// HTTP trigger payloads/responses — sealed records.
public sealed record SmokeTestStep(string Name, bool Succeeded, string? Detail);
```

> A handful of legacy command/query files under `Abstractions/Common/CQRS` are still block-scoped namespaces; the snippets above show the file-scoped target style. Don't churn the legacy files — migrate on touch.

**OData-bound domain entities are deliberately MUTABLE classes.** They inherit `BaseEntity<TKey>`, implement `GetCompositeKey()`, use `{ get; set; }` (some `virtual`), and carry `[JsonPropertyName]` for camelCase D365 wire names plus `[ODataField]` for create/update behaviour. Mutability is REQUIRED — PanoramicData.OData.Client materialises and round-trips entities via property setters. Do **not** convert these to records or `init`-only.

```csharp
[Table("LedgerJournalHeaders")]
public class LedgerJournalHeader : BaseEntity<string>
{
    [Key]
    [JsonPropertyName("dataAreaId")]
    public required string DataAreaId { get; set; }

    [Key]
    [JsonPropertyName("JournalBatchNumber")]
    [ODataField(IgnoreOnCreate = true)]            // server-generated
    public string? JournalBatchNumber { get; set; }

    [JsonPropertyName("JournalName")]
    public virtual required string JournalName { get; set; }

    public override object[] GetCompositeKey() => [DataAreaId, JournalBatchNumber ?? "null"];
}
```

The codebase DOES adopt `required` and collection expressions `[...]`. There are no `record struct` / `readonly record struct` value objects in this domain — it is network-bound, so zero-alloc value types add little.

### Nullable Reference Types

NRT is enabled solution-wide. Annotate; use `?.` / `??`. Guard nulls by returning `Result.Fail` (see Error Handling), not by throwing.

```csharp
public string? JournalBatchNumber { get; set; }
object[] key = request.CompositeKey ?? [];
```

### Async / Await

```csharp
// ConfigureAwait(false) in ALL library code (every project except the SampleFunction host).
return await _service.AddAsync(request.Entity, cancellationToken).ConfigureAwait(false);

// CancellationToken propagated through every async chain — no exceptions.
public async Task<Result<TEntity>> Handle(CreateCommand<TEntity> request, CancellationToken cancellationToken)
    => await _service.AddAsync(request.Entity, cancellationToken).ConfigureAwait(false);
```

**Avoid:** `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` (no sync-over-async); `Task.Run` in library code (let the caller decide threading); `async void`; dropping the `CancellationToken`.

> Convention note: the rule of record is "ConfigureAwait(false) everywhere except the host". In practice the SampleFunction host triggers also use it. New library code MUST use it; in the host it is harmless and currently present. See `perf-reliability.md` for the HTTP-timeout / cancellation reliability story (same `CancellationToken`).

### Primary Constructors (C# 12+)

Primary constructors are **preferred for new DI types** for brevity. They are NOT mandatory, and the existing explicit-constructor handlers (the majority pattern) are correct — do not churn them.

```csharp
// Preferred for new handlers/services:
public class CreateLedgerJournalHeaderHandler(IService<LedgerJournalHeader> service)
    : IRequestHandler<CreateLedgerJournalHeaderCommand, Result<LedgerJournalHeader>>;

// Existing explicit-ctor style is also fine — do NOT flag it as an anti-pattern:
public class CreateCommandHandler<TEntity> : IRequestHandler<CreateCommand<TEntity>, Result<TEntity>>
    where TEntity : class, IEntity
{
    private readonly IService<TEntity> _service;
    public CreateCommandHandler(IService<TEntity> service) => _service = service;
}
```

### Collection Expressions (C# 12+)

```csharp
public override object[] GetCompositeKey() => [DataAreaId, JournalBatchNumber ?? "null"];
missingRequired ??= [];
```

Prefer `[]` over `new List<T>()` / `new[] { ... }` in new code. A few legacy `new()` initialisers remain; replace opportunistically when you touch them.

### Pattern Matching

Used judiciously — `is null` / `is not null`, switch expressions for mapping. Don't over-pattern-match deep property chains.

```csharp
if (exception is not null) CausedBy(exception);
if (delimiter is null) return Result.Fail<DimensionOrder>(...);
```

### LINQ

Method syntax; `.Any()` not `.Count() > 0`; materialise with `.ToList()` when reusing a query.

```csharp
IntegrationError? error = result.Errors.OfType<IntegrationError>().FirstOrDefault();
return segments.Where(s => s.Length > 0).ToDictionary(s => s.Name, s => s.Value);
```

### Mappings

**No AutoMapper.** Write explicit mappings between entities, DTOs, and wire shapes. Do not wrap `ODataService<T>` in a repository — call it via `IMediator` (commands/queries), never directly from endpoints (architecture rule).

### Code Organisation

```csharp
// File-scoped namespace, one type per file.
namespace IntegratoR.OData.Common.Services;

// Expression-bodied members for simple getters.
public string FullName => $"{FirstName} {LastName}";
```

**Avoid:** block-scoped `namespace X { ... }` (a handful of legacy files still use it — migrate when you edit them); `#region`; multiple public types per file.

### `var` — explicit for non-obvious types

`var` is allowed when the type is obvious from the right-hand side (`var entity = new TestEntity { ... }`). Use an explicit type when the type is NOT apparent (interface returns, batch results, builder outputs).

```csharp
var entity = new LedgerJournalHeader { DataAreaId = "USMF" };                  // obvious — fine
IReadOnlyList<BatchOperationResult> results = await batch.ExecuteAsync(ct).ConfigureAwait(false);  // type not apparent — explicit
```

### Dependencies (Central Package Management)

All package versions live in `Directory.Packages.props`. `.csproj` files use `<PackageReference Include="..." />` with **no `Version` attribute**. When introducing a package, add a `<PackageVersion>` entry to `Directory.Packages.props`.

### Code Quality (YAGNI)

- No abstractions for one-time operations; no speculative "flexibility" or configurability that wasn't requested.
- No error handling for scenarios that cannot happen. Only validate at system boundaries (user input, external APIs).
- **Exception — published library API:** the carve-out noted under Core Principles applies. A lenient fallback on the public `IntegratoR.*` surface is intentional, not dead code (see `api-compatibility.md`).

## Testing Conventions

Stack: **xUnit v3 + NSubstitute + FluentAssertions + `IntegratoR.TestKit`**. No Moq. No raw `Assert.*`. Test projects mirror the source structure.

```csharp
public class CreateCommandHandlerTests
{
    private readonly IService<TestEntity> _service = Substitute.For<IService<TestEntity>>();
    private readonly CreateCommandHandler<TestEntity> _sut;

    public CreateCommandHandlerTests() => _sut = new CreateCommandHandler<TestEntity>(_service);

    [Fact]
    public async Task Handle_ServiceReturnsSuccess_ReturnsSuccessResult()
    {
        // Arrange
        var entity = new TestEntity { Id = "1", PartitionKey = "pk", Name = "Test" };
        var command = new CreateCommand<TestEntity>(entity);
        _service.AddAsync(entity, Arg.Any<CancellationToken>()).Returns(Result.Ok(entity));

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert — TestKit Result assertions
        result.Should().BeSuccessful();
        result.Value.Should().Be(entity);
    }

    [Fact]
    public async Task Handle_ServiceReturnsFailure_PropagatesErrors()
    {
        var entity = new TestEntity { Id = "1", PartitionKey = "pk", Name = "Test" };
        _service.AddAsync(entity, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TestEntity>("Entity.CreateFailed"));

        var result = await _sut.Handle(new CreateCommand<TestEntity>(entity), CancellationToken.None);

        // Failures are asserted via BeFailed() — there is no exception to catch.
        result.Should().BeFailed();
        result.Errors.Should().ContainSingle(e => e.Message == "Entity.CreateFailed");
    }

    [Fact]
    public async Task Handle_DelegatesToService()
    {
        var entity = new TestEntity { Id = "1", PartitionKey = "pk", Name = "Test" };
        _service.AddAsync(entity, Arg.Any<CancellationToken>()).Returns(Result.Ok(entity));

        await _sut.Handle(new CreateCommand<TestEntity>(entity), CancellationToken.None);

        await _service.Received(1).AddAsync(entity, Arg.Any<CancellationToken>());
    }
}
```

**TestKit Result assertions** (exact names — do NOT invent `BeSuccess()`/`BeFailure()`):

| Assertion | Asserts |
|---|---|
| `result.Should().BeSuccessful()` | `IsSuccess` is true |
| `result.Should().BeFailed()` | `IsFailed` is true |
| `result.Should().HaveErrorCode("Code")` | failed with an `IntegrationError` whose `Code` matches |
| `result.Should().HaveErrorType(ErrorType.Validation)` | failed with an `IntegrationError` of that `ErrorType` |
| `result.Should().HaveValue(expected)` | succeeded and value equals `expected` |

Other TestKit helpers: `FakeCacheService`, `FakeHttpMessageHandler`. NSubstitute idioms: `Substitute.For<T>()`, `.Returns(...)`, `.Received(n)`, `Arg.Any<T>()`.

**Test naming:** `Method_Scenario_Expected` (`Handle_ServiceReturnsFailure_PropagatesErrors`). AAA structure.

## Anti-Patterns (IntegratoR-specific)

- **Never** throw exceptions for business/validation flow — return `Result.Fail(new IntegrationError(...))`.
- **Never** guard-by-throw (`ArgumentNullException.ThrowIfNull`) at a library boundary — guard-by-`Result`.
- **Never** spell behaviour types `Behavior` — use British `Behaviour`.
- **Never** add a `Version` attribute in a `.csproj` — versions live in `Directory.Packages.props`.
- **Never** use AutoMapper — write explicit mappings.
- **Never** wrap `ODataService<T>` in a repository; call services via `IMediator`.
- **Never** convert OData-bound entities to records / `init`-only — they must stay mutable.
- **Never** use Moq or raw `Assert.*` — NSubstitute + FluentAssertions + TestKit only.
- **Never** use `var` for non-obvious types.
- **Never** use `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` / `Task.Run` in library code; **never** drop the `CancellationToken`; **never** omit `ConfigureAwait(false)` in library code.
- **Never** catch or throw base `Exception`; **never** leave an empty catch block.
- **Never** add comments/docstrings to code you did not change.

## Low-Relevance For This Domain (informational)

This is a network-bound OData/HTTP framework, so generic high-performance C# topics have near-zero footing here — apply only if a genuine need appears, don't retrofit: `Span<T>` / `Memory<T>` / `ArrayPool` / `FrozenSet` / `SearchValues` zero-allocation primitives (latency is dominated by D365 round-trips), `ValueTask` hot-path tuning, the C# 14 `field` keyword and `extension` members, and `readonly record struct` value objects (none in the domain by design).

## Quick Reference

```
Errors:      Result<T> always; never throw for flow; IntegrationError(code, message, ErrorType);
             guard-by-Result not guard-by-throw; throw only inside the exception->Result wrapper
Records:     records for CQRS commands/queries + HTTP DTOs; OData entities are MUTABLE classes
             (BaseEntity<TKey>, {get;set;}, [JsonPropertyName], [ODataField], required)
Naming:      PascalCase members, _camelCase fields, IPrefix interfaces, Async suffix;
             Behaviour (British); CreateCommand<T>/GetByKeyQuery<T>
Async:       ConfigureAwait(false) in libraries, CancellationToken everywhere, no sync-over-async,
             no Task.Run in libraries
Nullability: NRT solution-wide, ? annotations, ?. and ??, guard nulls via Result.Fail
Style:       file-scoped namespaces, [] collection expressions, required members, no var for
             non-obvious types, no AutoMapper, no repository over ODataService
Packages:    versions in Directory.Packages.props only — no Version attr in csproj
Testing:     xUnit v3 + NSubstitute + FluentAssertions + TestKit; Substitute.For/.Returns/.Received;
             BeSuccessful()/BeFailed()/HaveErrorCode()/HaveErrorType()/HaveValue(); Method_Scenario_Expected
Runtime:     net10.0, LangVersion=preview (C# 14), Nullable + ImplicitUsings enabled (Directory.Build.props)
```
