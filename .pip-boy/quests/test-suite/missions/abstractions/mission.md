---
codename: "abstractions"
title: "Abstractions Tests"
quest: "test-suite"
status: planned
complexity: "M"
depends_on: ["testkit"]
created: "2026-02-18"
updated: "2026-02-18"
---

## Objective

Create the `tests/IntegratoR.Abstractions.Tests/` project with comprehensive unit tests for the innermost layer of the IntegratoR framework. This covers 8 test classes totalling ~51 tests, all pure unit tests with zero external dependencies and no mocking:

1. **`BaseEntity<TKey>`** -- reflection-based `GetLoggingContext()` and abstract `GetCompositeKey()`
2. **`IntegrationError`** -- domain-specific error extending FluentResults `Error` with `Code`, `Type`, and optional `Exception`
3. **`ResultExtensions`** -- `GetError()` extraction and `Match()` pattern matching for generic and non-generic `Result`
4. **`ResultJsonConverter`** (non-generic) -- Newtonsoft.Json serialization/deserialization for `Result`
5. **`ResultJsonConverter<T>`** (generic) -- Newtonsoft.Json serialization/deserialization for `Result<T>`
6. **`ResultGenericJsonConverter`** -- dynamic converter using reflection for any `Result<T>` type
7. **6 CQRS command records** -- `CreateCommand<T>`, `CreateBatchCommand<T>`, `UpdateCommand<T>`, `UpdateBatchCommand<T>`, `DeleteCommand<T>`, `DeleteBatchCommand<T>`
8. **2 CQRS query records** -- `GetByKeyQuery<T>` and `GetByFilterQuery<T>`

## Approach

### 1. Project structure

The `.csproj` should already exist from the `testkit` mission as a shell. Add references to `IntegratoR.Abstractions` and `IntegratoR.TestKit`. Mirror source folder structure:

```
tests/IntegratoR.Abstractions.Tests/
  Domain/Entities/BaseEntityTests.cs
  Common/Results/IntegrationErrorTests.cs
  Common/Results/ResultExtensionsTests.cs
  Common/Results/ResultJsonConverterTests.cs
  Common/Results/ResultJsonConverterGenericTests.cs
  Common/Results/ResultGenericJsonConverterTests.cs
  Common/CQRS/Commands/CqrsCommandRecordTests.cs
  Common/CQRS/Queries/CqrsQueryRecordTests.cs
```

### 2. BaseEntityTests (5 tests)

Uses `TestEntity` and `TestSingleKeyEntity` from TestKit. Tests:
- `GetLoggingContext_AllPropertiesPopulated_ReturnsDictionaryWithAllPublicProperties`
- `GetLoggingContext_NullPropertyValue_ReplacesWithNewObject`
- `GetLoggingContext_EntityWithIndexedProperty_ExcludesIndexer` (use a local test entity with `this[int]`)
- `GetLoggingContext_EntityWithNoPublicProperties_ReturnsEmptyDictionary` (derive minimal entity)
- `GetCompositeKey_CompositeKeyEntity_ReturnsCorrectKeyArray`

Key: `BaseEntity.GetLoggingContext()` uses `BindingFlags.Public | BindingFlags.Instance`, filters `CanRead` + no index parameters. Null values are replaced with `new object()`.

### 3. IntegrationErrorTests (4 tests)

- `Constructor_WithAllParameters_SetsCodeTypeAndMessage`
- `Constructor_WithException_SetsCausedByAndExceptionProperty`
- `Constructor_WithoutException_ExceptionIsNull`
- `Constructor_AllErrorTypes_SetsCorrectType` -- `[Theory]` with `[InlineData]` for `Failure`, `Validation`, `NotFound`, `Conflict`

### 4. ResultExtensionsTests (10 tests)

`GetError()` tests:
- `GetError_ResultWithIntegrationError_ReturnsFirst`
- `GetError_ResultWithMultipleIntegrationErrors_ReturnsFirst`
- `GetError_ResultWithNonIntegrationError_ReturnsNull`
- `GetError_SuccessResult_ReturnsNull`

`Match<T, TOut>()` tests (generic):
- `Match_SuccessResult_InvokesOnSuccessWithValue`
- `Match_FailedWithIntegrationError_InvokesOnFailure`
- `Match_FailedWithoutIntegrationError_CreatesSyntheticErrorWithUnknownCode`

`Match<TOut>()` tests (non-generic):
- `Match_SuccessResult_InvokesOnSuccess`
- `Match_FailedWithIntegrationError_InvokesOnFailure`
- `Match_FailedWithoutIntegrationError_CreatesSyntheticErrorWithUnknownCode`

### 5. ResultJsonConverterTests (7 tests)

Use `Newtonsoft.Json.JsonConvert` with explicit `JsonSerializerSettings` containing only the converter under test.

- `WriteJson_SuccessResult_WritesIsSuccessTrueAndEmptyErrors`
- `WriteJson_FailedWithIntegrationError_WritesCodeMessageType`
- `WriteJson_FailedWithPlainError_WritesUnknownCodeAndFailureType`
- `ReadJson_SuccessJson_ReturnsOkResult`
- `ReadJson_FailedJson_ReturnsResultWithIntegrationErrors`
- `RoundTrip_SuccessResult_PreservesProperties`
- `RoundTrip_FailedResult_PreservesProperties`

### 6. ResultJsonConverterGenericTests (6 tests)

- `WriteJson_SuccessWithValue_WritesIsSuccessValueAndEmptyErrors`
- `WriteJson_FailedResult_WritesIsSuccessFalseWithErrors`
- `ReadJson_SuccessJsonWithValue_ReturnsOkResultWithValue`
- `ReadJson_FailedJson_ReturnsFailResult`
- `RoundTrip_SuccessWithComplexType_PreservesValue`
- `RoundTrip_FailedResult_PreservesErrors`

### 7. ResultGenericJsonConverterTests (7 tests)

- `CanConvert_ResultOfString_ReturnsTrue`
- `CanConvert_NonGenericResult_ReturnsFalse`
- `CanConvert_UnrelatedType_ReturnsFalse`
- `RoundTrip_ResultOfString_PreservesValue`
- `RoundTrip_ResultOfInt_PreservesValue`
- `RoundTrip_ResultOfComplexObject_PreservesValue`
- `RoundTrip_FailedResult_PreservesErrors`

### 8. CqrsCommandRecordTests (6 tests)

- `CreateCommand_GetLoggingContext_DelegatesToEntityGetLoggingContext`
- `CreateBatchCommand_GetLoggingContext_ReturnsDictionaryWithCount`
- `UpdateCommand_GetLoggingContext_DelegatesToEntity`
- `UpdateBatchCommand_GetLoggingContext_ReturnsDictionaryWithCount`
- `DeleteCommand_GetLoggingContext_DelegatesToEntity`
- `DeleteBatchCommand_GetLoggingContext_ReturnsDictionaryWithCount`

Use `TestEntityBuilder` to construct test entities.

### 9. CqrsQueryRecordTests (2 tests)

- `GetByKeyQuery_GetLoggingContext_ReturnsEntityTypeAndSerializedKeyValues` -- Verify `EntityType` is `typeof(TestEntity).Name` and `KeyValues` is `System.Text.Json`-serialized composite key.
- `GetByFilterQuery_GetLoggingContext_ReturnsEntityTypeAndFilterString` -- Verify `Filter` produces a readable expression string like `x => (x.Id == "test")`.

## Existing Code to Leverage

| What | Path |
|------|------|
| BaseEntity source | `IntegratoR.Abstractions/Domain/Entities/BaseEntity.cs` |
| IntegrationError source | `IntegratoR.Abstractions/Common/Results/IntegrationError.cs` |
| ResultExtensions source | `IntegratoR.Abstractions/Common/Results/ResultExtensions.cs` |
| ResultJsonConverter (all 3) | `IntegratoR.Abstractions/Common/Results/ResultJsonConverter.cs` |
| ErrorType enum | `IntegratoR.Abstractions/Common/Results/ErrorType.cs` |
| 6 command records | `IntegratoR.Abstractions/Common/CQRS/Commands/*.cs` |
| 2 query records | `IntegratoR.Abstractions/Common/CQRS/Queries/*.cs` |
| TestEntity/Builder (from TestKit) | `tests/IntegratoR.TestKit/` |
| Custom Result assertions (from TestKit) | `tests/IntegratoR.TestKit/Assertions/` |

## Edge Cases

1. **BaseEntity with null property value** -- `GetLoggingContext()` replaces null with `new object()`, not null itself. Assert the value is not null and is of type `object`.
2. **BaseEntity with indexed property** -- Create a local test entity with `this[int index]` and verify it is excluded from logging context.
3. **IntegrationError CausedBy chain** -- When exception is provided, verify `Reasons` collection contains the exception.
4. **ResultExtensions.Match failure without IntegrationError** -- Creates synthetic error with code "Unknown" and message from first error or "Unknown error".
5. **ResultJsonConverter with plain Error (not IntegrationError)** -- Serializes with code "Unknown" and type "Failure".
6. **ResultJsonConverter.ReadErrors with missing fields** -- Defaults: code="Unknown", message="Unknown error", type parsed as Failure.
7. **ResultGenericJsonConverter.CanConvert with non-generic Result** -- Must return false.
8. **GetByKeyQuery with null CompositeKey** -- `GetLoggingContext()` returns "null" string for KeyValues.
9. **GetByFilterQuery.GetLoggingContext** -- `Filter.ToString()` produces expression tree string.

## Expected File Changes

| Action | File Path |
|--------|-----------|
| CREATE | `tests/IntegratoR.Abstractions.Tests/Domain/Entities/BaseEntityTests.cs` |
| CREATE | `tests/IntegratoR.Abstractions.Tests/Common/Results/IntegrationErrorTests.cs` |
| CREATE | `tests/IntegratoR.Abstractions.Tests/Common/Results/ResultExtensionsTests.cs` |
| CREATE | `tests/IntegratoR.Abstractions.Tests/Common/Results/ResultJsonConverterTests.cs` |
| CREATE | `tests/IntegratoR.Abstractions.Tests/Common/Results/ResultJsonConverterGenericTests.cs` |
| CREATE | `tests/IntegratoR.Abstractions.Tests/Common/Results/ResultGenericJsonConverterTests.cs` |
| CREATE | `tests/IntegratoR.Abstractions.Tests/Common/CQRS/Commands/CqrsCommandRecordTests.cs` |
| CREATE | `tests/IntegratoR.Abstractions.Tests/Common/CQRS/Queries/CqrsQueryRecordTests.cs` |
| MODIFY | `tests/IntegratoR.Abstractions.Tests/IntegratoR.Abstractions.Tests.csproj` (may need project reference updates) |

## Done When

1. All 8 test classes exist mirroring the source project folder structure
2. All ~51 tests pass via `dotnet test`
3. `BaseEntityTests` covers: all properties populated, null replacement, indexed property exclusion, no-properties entity, composite key delegation
4. `IntegrationErrorTests` covers: full constructor, with/without exception, all 4 `ErrorType` values
5. `ResultExtensionsTests` covers: `GetError()` 4 scenarios, `Match()` generic 3 scenarios, `Match()` non-generic 3 scenarios
6. All 3 JSON converter test classes cover: WriteJson, ReadJson, and round-trip for both success and failure
7. `CqrsCommandRecordTests` covers: single commands delegate to entity, batch commands return count
8. `CqrsQueryRecordTests` covers: `GetByKeyQuery` returns entity type + serialized keys, `GetByFilterQuery` returns entity type + filter string
9. No mocking is used -- all tests are pure unit tests
10. Tests follow AAA pattern, `MethodName_Scenario_ExpectedResult` naming, British spelling ("Behaviour")
11. Tests use TestKit entities (TestEntity, TestSingleKeyEntity) -- not production D365 entities

## TDD Guidance

**Test framework**: xUnit.v3 with `[Fact]` and `[Theory]`/`[InlineData]`
**Assertions**: FluentAssertions 8.x (`.Should().BeTrue()`, `.Should().BeEquivalentTo()`)
**Custom assertions**: Use `ResultAssertions` from TestKit for `result.Should().BeSuccessful()` and `result.Should().BeFailed()`

**Key patterns**:
- For BaseEntity tests, create local test entities inheriting `BaseEntity<TKey>` for edge cases (indexer, no properties).
- For JSON converter tests, use `Newtonsoft.Json.JsonConvert.SerializeObject`/`DeserializeObject` with explicit `JsonSerializerSettings`.
- For `ResultGenericJsonConverter`, test `CanConvert()` directly and test serialization by registering in settings.
- For CQRS command tests, instantiate `TestEntity` via `TestEntityBuilder`, create the command record, call `GetLoggingContext()`, assert dictionary contents.
- For CQRS query tests, construct `GetByKeyQuery<TestEntity>(new object[] { "id", "pk" })` and verify `GetLoggingContext()` entries.
