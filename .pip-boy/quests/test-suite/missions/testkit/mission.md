---
codename: "testkit"
title: "TestKit Foundation"
quest: "test-suite"
status: completed
complexity: "L"
depends_on: []
created: "2026-02-18"
updated: "2026-02-18"
---

## Objective

Create the shared test infrastructure project (`IntegratoR.TestKit`) that every subsequent test project depends on. The IntegratoR framework currently has zero test coverage, and before any tests can be written, the test infrastructure must exist. This mission establishes:

1. **Test Entity Doubles** -- Concrete `BaseEntity<TKey>` subclasses (`TestEntity`, `TestSingleKeyEntity`, `TestEntityWithODataAttributes`) that serve as stand-ins for production D365 entities in generic handler tests. Production entities like `LedgerJournalHeader` must never appear in generic handler tests.
2. **Test Entity Builder** -- A fluent `TestEntityBuilder` with sensible defaults and chainable property overrides to reduce test setup noise.
3. **Custom FluentAssertions** -- `ResultAssertions` and `ResultAssertions<T>` enabling `.Should().BeSuccessful()`, `.BeFailed()`, `.HaveErrorCode()`, `.HaveErrorType()`, and `.HaveValue()` directly on `Result`/`Result<T>` objects.
4. **Fake Infrastructure** -- `FakeHttpMessageHandler` (queued responses, request tracking), `FakeCacheService` (in-memory `ICacheService`), and `TestCacheableQuery<T>` (configurable `ICacheableQuery<T>` for CachingBehaviour tests).
5. **Solution and Package Configuration** -- Test framework packages in `Directory.Packages.props`, `InternalsVisibleTo` for OData internals access, and all 6 test projects registered in the solution.

## Approach

### Step 1: Add test package versions to `Directory.Packages.props`

Add a `<ItemGroup Label="Testing">` section with pinned versions for xunit.v3 (3.0.x), xunit.runner.visualstudio (3.0.x), Microsoft.NET.Test.Sdk (18.x), NSubstitute (5.3.x), and FluentAssertions (8.x). Follow the existing labelled `<ItemGroup>` pattern (see `Label="Core"`, `Label="Resilience"`, etc.).

### Step 2: Create `tests/IntegratoR.TestKit/IntegratoR.TestKit.csproj`

Class library (not a test project). References `IntegratoR.Abstractions` (for `BaseEntity<TKey>`, `IEntity`, `ICacheService`, `ICacheableQuery<T>`, `IntegrationError`, `ErrorType`), `IntegratoR.OData` (for `ODataFieldAttribute`), `FluentAssertions` (for custom assertion base classes), and `FluentResults`. Targets `net10.0` with `LangVersion preview`.

### Step 3: Test Entity Doubles (`Doubles/Entities/`)

- **`TestEntity : BaseEntity<string>`** -- Properties: `string Id` (required), `string PartitionKey` (required), `string Name` (required), `string? Description`. `GetCompositeKey()` returns `[Id, PartitionKey]`. Mirrors the D365 composite key pattern from `LedgerJournalHeader`.
- **`TestSingleKeyEntity : BaseEntity<int>`** -- Properties: `int Id` (required), `string Name` (required). `GetCompositeKey()` returns `[Id]`.
- **`TestEntityWithODataAttributes : BaseEntity<string>`** -- Properties: `string Id` with `[ODataField(IgnoreOnCreate = true)]`, `string Name`, `string ReadOnlyField` with `[ODataField(IgnoreOnUpdate = true)]`, `string? Mutable`. `GetCompositeKey()` returns `[Id]`.

### Step 4: Test Entity Builder (`Builders/TestEntityBuilder.cs`)

Fluent API with `.WithId()`, `.WithPartitionKey()`, `.WithName()`, `.WithDescription()`, `.Build()`. Defaults: `Id = "test-id"`, `PartitionKey = "test-partition"`, `Name = "Test Entity"`, `Description = null`. Static factory `TestEntityBuilder.Default()`. Mutable-return-this pattern.

### Step 5: Custom FluentAssertions (`Assertions/`)

- **`ResultAssertionExtensions.cs`** -- `public static ResultAssertions Should(this Result result)` and `public static ResultAssertions<T> Should<T>(this Result<T> result)`.
- **`ResultAssertions.cs`** -- `BeSuccessful()`, `BeFailed()`, `HaveErrorCode(string)`, `HaveErrorType(ErrorType)` returning `AndConstraint<>` for chaining. Uses `Execute.Assertion` for proper failure messages.
- **`ResultAssertions{T}.cs`** -- All of the above plus `HaveValue(T expected)`.

### Step 6: Fake Infrastructure (`Fakes/`)

- **`FakeHttpMessageHandler : HttpMessageHandler`** -- `Queue(HttpResponseMessage)`, `Queue(HttpStatusCode, string?)`, override `SendAsync()` (dequeues FIFO, throws `InvalidOperationException` on empty queue), `SentRequests` (IReadOnlyList), `CreateClient()`.
- **`FakeCacheService : ICacheService`** -- Backed by `Dictionary<string, object>`. Implements `GetAsync<T>`, `SetAsync<T>`, `RemoveAsync`. Test helpers: `Contains(string)`, `Count`, `Clear()`.
- **`TestCacheableQuery<T> : ICacheableQuery<T>`** -- Constructor takes `cacheKey`, `cacheDuration`, `cacheKeyValues`. Implements `GetLoggingContext()` and all `ICacheableQuery<T>` members.

### Step 7: Create 5 test project `.csproj` shells

Each xUnit test project references: Microsoft.NET.Test.Sdk, xunit.v3, xunit.runner.visualstudio, FluentAssertions, NSubstitute, IntegratoR.TestKit (project ref), their corresponding source project (project ref). Set `<IsPackable>false</IsPackable>` and `<IsTestProject>true</IsTestProject>`.

### Step 8: Add `InternalsVisibleTo` to `IntegratoR.OData.csproj`

Add `<InternalsVisibleTo Include="IntegratoR.OData.Tests" />` for access to internal types (`OperationContext`, `ODataNotFoundException`).

### Step 9: Update `IntegratoR.sln`

Add all 6 new projects under a `tests` solution folder.

## Existing Code to Leverage

| Source File | What to Reference |
|---|---|
| `IntegratoR.Abstractions/Domain/Entities/BaseEntity.cs` | `BaseEntity<TKey>` -- base class for test entities; `GetCompositeKey()` and reflection-based `GetLoggingContext()` |
| `IntegratoR.Abstractions/Interfaces/Entity/IEntity.cs` | `IEntity` -- contract that all test entities fulfill |
| `IntegratoR.Abstractions/Interfaces/Telemetry/IContext.cs` | `IContext` -- `GetLoggingContext()` contract |
| `IntegratoR.Abstractions/Interfaces/Services/ICacheService.cs` | `ICacheService` -- interface for `FakeCacheService` (`GetAsync<T>`, `SetAsync<T>`, `RemoveAsync`) |
| `IntegratoR.Abstractions/Interfaces/Queries/ICacheableQuery.cs` | `ICacheableQuery<T>` -- interface for `TestCacheableQuery<T>` (`CacheKey`, `CacheDuration`, `GetCacheKeyValues()`, `GenerateCacheKey()`) |
| `IntegratoR.Abstractions/Common/Results/IntegrationError.cs` | `IntegrationError` with `Code`, `Type`, `Exception?` -- needed for custom assertions |
| `IntegratoR.Abstractions/Common/Results/ErrorType.cs` | `ErrorType` enum -- needed for `HaveErrorType()` assertion |
| `IntegratoR.Abstractions/Common/Results/ResultExtensions.cs` | `GetError()` extension -- use in assertions to extract `IntegrationError` |
| `IntegratoR.OData/Common/Annotations/ODataFieldAttribute.cs` | `ODataFieldAttribute` -- `IgnoreOnCreate`, `IgnoreOnUpdate` for test entity |
| `IntegratoR.OData.FO/Domain/Entities/LedgerJournal/LedgerJournalHeader.cs` | Reference entity pattern: `[Key]`, `[JsonPropertyName]`, `[ODataField]`, `required`, composite key |
| `Directory.Packages.props` | Central package version management |
| `Directory.Build.props` | Shared project properties (`net10.0`, `LangVersion preview`, `Nullable enable`) |

## Edge Cases

- **FluentAssertions `Should()` collision**: `Result` and `Result<T>` already have generic `Should()` from FluentAssertions object assertions. The custom extension methods must be in a specific namespace that takes precedence. Verify extensions compile without ambiguity.
- **`FakeHttpMessageHandler` empty queue**: `SendAsync` called with an empty queue must throw `InvalidOperationException` with message "No more queued responses. Call Queue() before making HTTP requests."
- **`FakeCacheService` generic type handling**: `GetAsync<T>` may receive a type mismatch. Use `is T typed ? typed : default` pattern.
- **`TestCacheableQuery<T>` must implement `IContext`**: `ICacheableQuery<T>` extends `IQuery<T>` which extends `IRequest<T>, IContext`. Ensure `GetLoggingContext()` is implemented.
- **Test entities `required` properties vs. builder**: The builder must always set `required` properties. `Build()` must produce a valid entity without any explicit `With*` calls.
- **Namespace conventions**: Use `IntegratoR.TestKit.Doubles.Entities`, `IntegratoR.TestKit.Builders`, `IntegratoR.TestKit.Assertions`, `IntegratoR.TestKit.Fakes`.

## Expected File Changes

### New Files

| Path | Description |
|---|---|
| `tests/IntegratoR.TestKit/IntegratoR.TestKit.csproj` | Shared test infrastructure class library |
| `tests/IntegratoR.TestKit/Doubles/Entities/TestEntity.cs` | Composite key test entity |
| `tests/IntegratoR.TestKit/Doubles/Entities/TestSingleKeyEntity.cs` | Single key test entity |
| `tests/IntegratoR.TestKit/Doubles/Entities/TestEntityWithODataAttributes.cs` | Entity with `[ODataField]` attributes |
| `tests/IntegratoR.TestKit/Builders/TestEntityBuilder.cs` | Fluent builder for `TestEntity` |
| `tests/IntegratoR.TestKit/Assertions/ResultAssertionExtensions.cs` | Extension methods for `Result` to custom assertions |
| `tests/IntegratoR.TestKit/Assertions/ResultAssertions.cs` | Custom assertions for non-generic `Result` |
| `tests/IntegratoR.TestKit/Assertions/ResultAssertions{T}.cs` | Custom assertions for generic `Result<T>` |
| `tests/IntegratoR.TestKit/Fakes/FakeHttpMessageHandler.cs` | Queued HTTP response handler |
| `tests/IntegratoR.TestKit/Fakes/FakeCacheService.cs` | In-memory `ICacheService` implementation |
| `tests/IntegratoR.TestKit/Fakes/TestCacheableQuery.cs` | Configurable `ICacheableQuery<T>` test double |
| `tests/IntegratoR.Abstractions.Tests/IntegratoR.Abstractions.Tests.csproj` | Test project shell |
| `tests/IntegratoR.Application.Tests/IntegratoR.Application.Tests.csproj` | Test project shell |
| `tests/IntegratoR.OData.Tests/IntegratoR.OData.Tests.csproj` | Test project shell |
| `tests/IntegratoR.OData.FO.Tests/IntegratoR.OData.FO.Tests.csproj` | Test project shell |
| `tests/IntegratoR.RELion.Tests/IntegratoR.RELion.Tests.csproj` | Test project shell |

### Modified Files

| Path | Change |
|---|---|
| `Directory.Packages.props` | Add `<ItemGroup Label="Testing">` with xunit.v3, NSubstitute, FluentAssertions, Microsoft.NET.Test.Sdk |
| `IntegratoR.OData/IntegratoR.OData.csproj` | Add `<InternalsVisibleTo Include="IntegratoR.OData.Tests" />` |
| `IntegratoR.sln` | Add 6 test projects + `tests` solution folder |

## Done When

1. `dotnet build` succeeds with zero errors across all 7 projects (TestKit + 5 test projects + unchanged source projects)
2. `Directory.Packages.props` contains xunit.v3, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, NSubstitute, and FluentAssertions with pinned versions in a `Testing` labelled item group
3. `IntegratoR.OData.csproj` contains `<InternalsVisibleTo Include="IntegratoR.OData.Tests" />`
4. `IntegratoR.sln` includes all 6 test projects under a `tests` solution folder
5. `TestEntity` inherits `BaseEntity<string>`, has `required` properties `Id`, `PartitionKey`, `Name`, and `GetCompositeKey()` returns `[Id, PartitionKey]`
6. `TestSingleKeyEntity` inherits `BaseEntity<int>`, has `required` properties `Id`, `Name`, and `GetCompositeKey()` returns `[Id]`
7. `TestEntityWithODataAttributes` has at least one property with `[ODataField(IgnoreOnCreate = true)]` and one with `[ODataField(IgnoreOnUpdate = true)]`
8. `TestEntityBuilder.Default().Build()` returns a valid `TestEntity` without requiring any explicit `With*` calls
9. `ResultAssertions` provides `BeSuccessful()`, `BeFailed()`, `HaveErrorCode(string)`, `HaveErrorType(ErrorType)` -- all returning `AndConstraint<>` for chaining
10. `ResultAssertions<T>` provides all of the above plus `HaveValue(T)`
11. `FakeHttpMessageHandler` can queue responses, dequeue FIFO via `SendAsync`, and exposes `SentRequests`
12. `FakeCacheService` implements `ICacheService` with `GetAsync<T>`, `SetAsync<T>`, `RemoveAsync` backed by an in-memory dictionary, plus test helpers `Contains`, `Count`, `Clear`
13. `TestCacheableQuery<T>` implements `ICacheableQuery<T>` with configurable `CacheKey`, `CacheDuration`, and `GetCacheKeyValues()`
14. All `.cs` files use file-scoped namespaces, XML doc comments on public members, and British spelling where applicable
15. No production source code is modified beyond `IntegratoR.OData.csproj` (InternalsVisibleTo) and solution/package config files

## TDD Guidance

This mission creates test infrastructure, not tests themselves. Verification is compilation-based: if `dotnet build` succeeds, the type system validates that test entities implement `BaseEntity<TKey>`/`IEntity`/`IContext`, `FakeCacheService` satisfies `ICacheService`, `TestCacheableQuery<T>` satisfies `ICacheableQuery<T>`, and all project references resolve.

Optional smoke tests in `tests/IntegratoR.TestKit.Tests/`:
- `TestEntityBuilder_Default_Build_ReturnsEntityWithDefaults`
- `FakeHttpMessageHandler_QueuedResponse_ReturnsFIFO`
- `FakeHttpMessageHandler_EmptyQueue_ThrowsInvalidOperationException`
- `FakeCacheService_SetAndGet_ReturnsCachedValue`
- `ResultAssertions_BeSuccessful_OnSuccessResult_DoesNotThrow`
- `ResultAssertions_HaveErrorCode_MatchesIntegrationError`

**Frameworks**: xUnit.v3 (3.0.x), FluentAssertions (8.x), NSubstitute (5.3.x). No mocking needed for TestKit itself.
