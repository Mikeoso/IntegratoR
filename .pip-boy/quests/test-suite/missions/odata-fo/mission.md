---
codename: "odata-fo"
title: "OData.FO Tests"
quest: "test-suite"
status: completed
complexity: "M"
depends_on: ["testkit", "odata"]
created: "2026-02-18"
updated: "2026-02-18"
---

## Purpose

Test D365 Finance & Operations entities, composite keys, financial dimension builder, dimension query/handler, F&O-specific CQRS handlers (Create/Update for headers and lines), command logging contexts, and DI registration. ~56 tests, mostly small complexity.

## Objective

The `IntegratoR.OData.FO` project contains the D365 Finance & Operations domain model and CQRS handlers. Unlike the generic `OData` project, most classes here are concrete domain logic (entities, builders, validators) with minimal external dependencies. This mission creates comprehensive tests for all 12 groups of production code:

1. **Entity composite keys** (3 groups, 7 tests) -- `LedgerJournalHeader`, `LedgerJournalLine`, and dimension entities (`DimensionIntegrationFormat`, `DimensionParameters`) verifying `GetCompositeKey()` returns correct field arrays and `GetLoggingContext()` captures public properties.
2. **FinancialDimensionBuilder** (8 tests) -- Pure logic builder class verifying segment ordering, delimiter joining, missing segment placeholders, null/whitespace input handling, and state reset.
3. **DimensionSegmentDelimiterExtensions** (3 tests) -- Extension method mapping enum values to char representations with edge case for unsupported/null values.
4. **GetDimensionOrdersQuery** (3 tests) -- Record verifying cache key composition, cache duration, and logging context.
5. **GetDimensionOrdersQueryValidator** (4 tests) -- FluentValidation rules for dimension format and hierarchy type.
6. **GetDimensionOrdersQueryHandler** (5 tests) -- MediatR handler coordinating two OData service calls, parsing dimension format strings, and building `DimensionFormat` result.
7. **F&O Create handlers** (8 tests) -- 4 handlers (header single, header batch, line single, line batch) each with success/failure paths.
8. **F&O Update handlers** (8 tests) -- 4 handlers (header single, header batch, line single, line batch) each with success/failure paths.
9. **Command logging contexts** (8 tests) -- 8 command records verifying `GetLoggingContext()` delegation and dictionary construction.
10. **F&O DependencyInjection** (2 tests) -- DI registration for MediatR handlers and FOSettings configuration.

## Approach

### Step 1: Create test project structure

Create `tests/IntegratoR.OData.FO.Tests/` with folder structure mirroring source:

```
tests/IntegratoR.OData.FO.Tests/
  Domain/
    Entities/
      LedgerJournal/
        LedgerJournalHeaderTests.cs
        LedgerJournalLineTests.cs
      Dimensions/
        DimensionEntityTests.cs
  Builders/
    FinancialDimensionBuilderTests.cs
  Common/
    Extensions/
      DimensionSegmentDelimiterExtensionsTests.cs
      ApplicationDependencyInjectionTests.cs
  Features/
    Queries/
      Dimensions/
        GetDimensionOrder/
          GetDimensionOrdersQueryTests.cs
          GetDimensionOrdersQueryValidatorTests.cs
          GetDimensionOrdersQueryHandlerTests.cs
    Commands/
      LedgerJournals/
        CreateLedgerJournalHeader/
          CreateLedgerJournalHeaderHandlerTests.cs
        CreateLedgerJournalLine/
          CreateLedgerJournalLineHandlerTests.cs
        UpdateLedgerJournalHeader/
          UpdateLedgerJournalHeaderHandlerTests.cs
        UpdateLedgerJournalLine/
          UpdateLedgerJournalLineHandlerTests.cs
        CommandLoggingContextTests.cs
```

The `.csproj` is created by the `testkit` mission. It references `IntegratoR.OData.FO` (project), `IntegratoR.TestKit` (project), xUnit.v3, NSubstitute, FluentAssertions, Microsoft.NET.Test.Sdk.

### Step 2: LedgerJournalHeaderTests (3 tests)

**Source**: `IntegratoR.OData.FO/Domain/Entities/LedgerJournal/LedgerJournalHeader.cs`

No mocks needed -- pure entity construction and method calls.

**Tests**:
1. `GetCompositeKey_AllFieldsSet_ReturnsDataAreaIdAndJournalBatchNumber` -- Create header with `DataAreaId = "USMF"`, `JournalBatchNumber = "GJ001"`. Assert `GetCompositeKey()` returns `["USMF", "GJ001"]`.
2. `GetCompositeKey_NullJournalBatchNumber_ReturnsNullStringLiteral` -- Leave `JournalBatchNumber` as null. Assert key contains `"null"` string (the code does `JournalBatchNumber ?? "null"`).
3. `GetLoggingContext_ReturnsAllPublicProperties` -- Call `GetLoggingContext()`, verify it returns a dictionary containing keys for public properties (inherited from `BaseEntity.GetLoggingContext()` which uses reflection).

### Step 3: LedgerJournalLineTests (2 tests)

**Source**: `IntegratoR.OData.FO/Domain/Entities/LedgerJournal/LedgerJournalLine.cs`

**Tests**:
1. `GetCompositeKey_AllFieldsSet_ReturnsThreePartKey` -- Create line with `DataAreaId = "USMF"`, `JournalBatchNumber = "GJ001"`, `LineNumber = 1.0m`. Assert `GetCompositeKey()` returns `["USMF", "GJ001", 1.0m]`.
2. `GetLoggingContext_ReturnsAllPublicProperties` -- Verify logging context dictionary contains expected property names.

### Step 4: DimensionEntityTests (2 tests)

**Source**: `IntegratoR.OData.FO/Domain/Entities/Dimensions/DimensionIntegrationFormat.cs` and `DimensionParameters.cs`

**Tests**:
1. `DimensionIntegrationFormat_GetCompositeKey_ReturnsFormatNameAndType` -- Create with `DimensionFormatName = "DefaultFormat"`, `DimensionFormatType = DimensionHierarchyType.DataEntityDefaultDimensionFormat`. Assert key is `["DefaultFormat", DimensionHierarchyType.DataEntityDefaultDimensionFormat]`.
2. `DimensionParameters_GetCompositeKey_ReturnsKey` -- Create with `Key = "Default"`. Assert key is `["Default"]`.

### Step 5: FinancialDimensionBuilderTests (8 tests)

**Source**: `IntegratoR.OData.FO/Builders/FinancialDimensionBuilder.cs`

Pure logic -- no mocks. Create `DimensionFormat` instances with known segments and delimiters.

**Tests**:
1. `Build_NotInitialized_ReturnsEmptyString` -- Call `Build()` without `Initialize()`, verify empty string.
2. `Build_AllSegmentsProvided_JoinsWithDelimiter` -- Initialize with format `{ Delimiter = "-", Segments = ["BU", "Dept", "CC"] }`, add all three segments, verify `"BU01-D001-CC002"`.
3. `Build_MissingMiddleSegment_InsertsEmptyPlaceholder` -- Provide BU and CC but not Dept, verify `"BU01--CC002"` (empty placeholder between delimiters).
4. `Build_AddedOutOfOrder_RespectsFormatOrder` -- Add CC first, then BU -- output still respects format segment order.
5. `Build_SingleSegment_NoDelimiter` -- Format with one segment only, verify no delimiter in output.
6. `Add_NullOrWhitespaceName_IgnoresEntry` -- Call `Add(null, "value")` and `Add("  ", "value")`, verify they do not appear in output.
7. `Clear_AfterAdditions_ResetsState` -- Add segments, call `Clear()`, call `Build()`, verify empty string (format is cleared too).
8. `Initialize_AfterPreviousUse_ClearsState` -- Add segments, call `Initialize()` with new format, verify old segments are gone.

### Step 6: DimensionSegmentDelimiterExtensionsTests (3 tests)

**Source**: `IntegratoR.OData.FO/Common/Extensions/DimensionSegmentDelimiterExtensions.cs`

**Tests**:
1. `GetCharValue_Hyphen_ReturnsHyphenChar` -- `DimensionSegmentDelimiter.Hyphen.GetCharValue()` returns `'-'`.
2. `GetCharValue_UnsupportedEnum_ThrowsArgumentOutOfRangeException` -- Pass `DimensionSegmentDelimiter.Period` (not handled in switch), verify `ArgumentOutOfRangeException`. Note: the current implementation only handles `Hyphen` -- all other values fall through to default which throws.
3. `GetCharValue_Null_ThrowsArgumentOutOfRangeException` -- Pass `null` (the parameter is nullable `DimensionSegmentDelimiter?`), verify it throws.

**Implementation note**: The extension method currently only handles `Hyphen`. All other enum values (Period, Underscore, Bar, etc.) throw. Use `[Theory]` with `[InlineData]` for the unsupported values.

### Step 7: GetDimensionOrdersQueryTests (3 tests)

**Source**: `IntegratoR.OData.FO/Features/Queries/Dimensions/GetDimensionOrder/GetDimensionOrdersQuery.cs`

**Tests**:
1. `CacheKey_ContainsQueryNameFormatAndHierarchyType` -- Create query with `dimensionFormat = "DefaultFormat"`, `hierarchyType = DataEntityDefaultDimensionFormat`. Assert `CacheKey` equals `"GetDimensionOrdersQuery-DefaultFormat-DataEntityDefaultDimensionFormat"`.
2. `CacheDuration_Is15Minutes` -- Assert `CacheDuration` is `TimeSpan.FromMinutes(15)`.
3. `GetLoggingContext_ContainsDimensionFormatAndHierarchyType` -- Assert returned dictionary has keys `"DimensionFormat"` and `"HierarchyType"` with correct values.

### Step 8: GetDimensionOrdersQueryValidatorTests (4 tests)

**Source**: `IntegratoR.OData.FO/Features/Queries/Dimensions/GetDimensionOrder/GetDimensionOrdersQueryValidator.cs`

Use FluentValidation's `TestValidate()` extension method for clean test syntax.

**Tests**:
1. `Validate_ValidQuery_HasNoErrors` -- Valid query passes all rules.
2. `Validate_EmptyDimensionFormat_HasError` -- Empty string `dimensionFormat` triggers `NotEmpty` rule.
3. `Validate_DimensionFormatExceeds100Chars_HasError` -- 101-character string triggers `MaximumLength` rule.
4. `Validate_InvalidHierarchyType_HasError` -- Cast invalid int (e.g., `(DimensionHierarchyType)999`) triggers `IsInEnum` rule.

### Step 9: GetDimensionOrdersQueryHandlerTests (5 tests)

**Source**: `IntegratoR.OData.FO/Features/Queries/Dimensions/GetDimensionOrder/GetDimensionOrdersQueryHandler.cs`

**Mock setup**:
- `ILogger<GetDimensionOrdersQueryHandler>` -- NSubstitute mock
- `IODataService<DimensionParameters>` -- mock `FindAll()` returning `Result.Ok<IEnumerable<DimensionParameters>>(list)` with a `DimensionSegmentDelimiter.Hyphen`
- `IService<DimensionIntegrationFormat>` -- mock `FindAsync()` returning format records with `FinancialDimensionFormat = "MainAccount-BU-Dept"`

**Tests**:
1. `Handle_ValidRequest_ReturnsDimensionFormatWithSegmentsAndDelimiter` -- Both service calls succeed, verify returned `DimensionFormat` has correct delimiter and segment list `["MainAccount", "BU", "Dept"]`.
2. `Handle_DimensionFormatsQueryFails_ReturnsFailure` -- `FindAsync` returns `Result.Fail`, verify handler returns failure with error code `DimensionParameters.QueryFailed`.
3. `Handle_DimensionParametersQueryFails_ReturnsFailure` -- `FindAll` returns `Result.Fail`, verify handler returns failure.
4. `Handle_NoDimensionFormats_ReturnsError` -- `FindAsync` succeeds but returns empty collection, verify handler processes gracefully (note: current code does `FirstOrDefault()` which returns null, then calls `.Split()` on null -- this may indicate a bug to document).
5. `Handle_ParsesFinancialDimensionFormatString_IntoSegments` -- Verify that `"MainAccount-BU-Dept"` split by `-` produces `["MainAccount", "BU", "Dept"]` as segments in the result.

**Edge case note**: The handler calls `dimensionDelimiter.GetCharValue()` which only supports `Hyphen`. If the delimiter is another enum value, it throws. Tests should document this limitation.

### Step 10: FOCreateHandlerTests (8 tests, 4 handlers)

**Source**: `IntegratoR.OData.FO/Features/Commands/LedgerJournals/Create*/` (4 handler files)

All F&O handlers follow the same pattern: inject `IService<T>` or `IODataBatchService<T>` + `ILogger`, call the service method, return the mapped result. Use `LedgerJournalHeader` and `LedgerJournalLine` directly (these are F&O-specific tests, not generic).

**CreateLedgerJournalHeaderHandler** (single):
1. `Handle_Success_ReturnsOkWithEntity` -- `_service.AddAsync()` returns `Result.Ok(entity)`, verify handler returns success with entity
2. `Handle_Failure_ReturnsError` -- `_service.AddAsync()` returns `Result.Fail(error)`, verify handler propagates failure

**CreateLedgerJournalHeadersHandler** (batch):
3. `Handle_Success_ReturnsOk` -- `_service.AddBatchAsync()` returns `Result.Ok()`, verify handler returns success
4. `Handle_Failure_ReturnsError` -- `_service.AddBatchAsync()` returns `Result.Fail(error)`, verify failure

**CreateLedgerJournalLineHandler** (single):
5. `Handle_Success_ReturnsOkWithEntity` -- Same pattern with `LedgerJournalLine`
6. `Handle_Failure_ReturnsError`

**CreateLedgerJournalLinesHandler** (batch):
7. `Handle_Success_ReturnsOk`
8. `Handle_Failure_ReturnsError`

**Mock setup pattern** (applies to all handlers):
```csharp
var service = Substitute.For<IService<LedgerJournalHeader>>();
var logger = Substitute.For<ILogger<CreateLedgerJournalHeaderHandler<LedgerJournalHeader>>>();
var handler = new CreateLedgerJournalHeaderHandler<LedgerJournalHeader>(logger, service);

var entity = new LedgerJournalHeader { DataAreaId = "USMF", JournalName = "GenJnl", Description = "Test" };
var command = new CreateLedgerJournalHeaderCommand<LedgerJournalHeader>(entity);

service.AddAsync(entity, Arg.Any<CancellationToken>()).Returns(Result.Ok(entity));
var result = await handler.Handle(command, CancellationToken.None);
result.Should().BeSuccessful();
```

### Step 11: FOUpdateHandlerTests (8 tests, 4 handlers)

**Source**: `IntegratoR.OData.FO/Features/Commands/LedgerJournals/Update*/` (4 handler files)

Same pattern as create handlers but with `UpdateAsync`/`UpdateBatchAsync`.

**UpdateLedgerJournalHeaderHandler** (single):
1. `Handle_Success_ReturnsOkWithEntity` -- `_service.UpdateAsync()` returns success
2. `Handle_Failure_ReturnsError` -- Returns failure

**UpdateLedgerJournalHandler** (batch -- note class name differs: `UpdateLedgerJournalHandler`, not `UpdateLedgerJournalHeadersHandler`):
3. `Handle_Success_ReturnsOk`
4. `Handle_Failure_ReturnsError`

**UpdateLedgerJournalLineHandler** (single):
5. `Handle_Success_ReturnsOkWithEntity`
6. `Handle_Failure_ReturnsError`

**UpdateLedgerJournalLinesHandler** (batch):
7. `Handle_Success_ReturnsOk`
8. `Handle_Failure_ReturnsError`

**Implementation note**: The batch update header handler class is named `UpdateLedgerJournalHandler<TEntity>` (not `UpdateLedgerJournalHeadersHandler`) -- take care with the class name when writing tests.

### Step 12: FOCommandLoggingContextTests (8 tests)

**Source**: All command record files across Create and Update features.

No mocks -- pure record instantiation and `GetLoggingContext()` verification.

**Tests**:
1. `CreateLedgerJournalHeaderCommand_GetLoggingContext_DelegatesToEntity` -- `CreateLedgerJournalHeaderCommand<T>` inherits from `CreateCommand<T>` which calls `entity.GetLoggingContext()`. Verify the returned dictionary contains entity properties.
2. `CreateLedgerJournalHeadersCommand_GetLoggingContext_ReturnsCountAndJournalNames` -- Override returns `{ "EntityType", "Count", "JournalNames" }`. Verify with 2 entities that Count is 2 and JournalNames is comma-separated.
3. `CreateLedgerJournalLineCommand_GetLoggingContext_DelegatesToEntity` -- Delegates to `LedgerJournalLine.GetLoggingContext()`.
4. `CreateLedgerJournalLinesCommand_GetLoggingContext_ReturnsCountAndBatchNumbers` -- Returns `{ "Count", "JournalNames" }` (note: the property key is "JournalNames" but values are `JournalBatchNumber`).
5. `UpdateLedgerJournalHeaderCommand_GetLoggingContext_DelegatesToEntity` -- Delegates to `LedgerJournalHeader.GetLoggingContext()`.
6. `UpdateLedgerJournalHeadersCommand_GetLoggingContext_ReturnsCountAndJournalNames` -- Returns `{ "Count", "JournalNames" }`.
7. `UpdateLedgerJournalLineCommand_GetLoggingContext_DelegatesToEntity` -- Delegates to `LedgerJournalLine.GetLoggingContext()`.
8. `UpdateLedgerJournalLinesCommand_GetLoggingContext_ReturnsCountAndBatchNumbers` -- Returns `{ "Count", "JournalBatchNumbers" }` (key is "JournalBatchNumbers", distinct batch numbers joined by comma).

### Step 13: FODependencyInjectionTests (2 tests)

**Source**: `IntegratoR.OData.FO/Common/Extensions/ApplicationDependencyInjection.cs`

**Tests**:
1. `AddODataClientFOProxy_RegistersMediatR_WithGenericHandlers` -- Call `AddODataClientFOProxy(config)`, verify MediatR services are registered (check `IMediator` or handler types can be resolved).
2. `AddODataClientFOProxy_ConfiguresFOSettings` -- Build config with `"FOSettings:DimensionFormatName": "DefaultFormat"`, call `AddODataClientFOProxy(config)`, resolve `IOptions<FOSettings>`, verify `DimensionFormatName`.

## Existing Code to Leverage

| Source File | What to Reference |
|---|---|
| `IntegratoR.OData.FO/Domain/Entities/LedgerJournal/LedgerJournalHeader.cs` | Composite key `[DataAreaId, JournalBatchNumber ?? "null"]`, `required` properties, `[ODataField(IgnoreOnCreate = true)]` on JournalBatchNumber |
| `IntegratoR.OData.FO/Domain/Entities/LedgerJournal/LedgerJournalLine.cs` | Three-part composite key `[DataAreaId, JournalBatchNumber, LineNumber]`, many `[ODataField(IgnoreOnCreate = true)]` properties |
| `IntegratoR.OData.FO/Domain/Entities/Dimensions/DimensionIntegrationFormat.cs` | Two-part composite key `[DimensionFormatName, DimensionFormatType]`, `IsActive` enum property |
| `IntegratoR.OData.FO/Domain/Entities/Dimensions/DimensionParameters.cs` | Single key `[Key]`, `DimensionSegmentDelimiter` enum property |
| `IntegratoR.OData.FO/Builders/FinancialDimensionBuilder.cs` | `Initialize(DimensionFormat)`, `Add(name, value)`, `Build()`, `Clear()` -- uses `Dictionary<string, string>` internally |
| `IntegratoR.OData.FO/Common/Extensions/DimensionSegmentDelimiterExtensions.cs` | Only handles `Hyphen` case, all others throw `ArgumentOutOfRangeException` |
| `IntegratoR.OData.FO/Features/Queries/Dimensions/GetDimensionOrder/GetDimensionOrdersQuery.cs` | Record with `CacheKey`, `CacheDuration = 15min`, `GetLoggingContext()` |
| `IntegratoR.OData.FO/Features/Queries/Dimensions/GetDimensionOrder/GetDimensionOrdersQueryValidator.cs` | `NotEmpty`, `MaximumLength(100)`, `IsInEnum` rules |
| `IntegratoR.OData.FO/Features/Queries/Dimensions/GetDimensionOrder/GetDimensionOrdersQueryHandler.cs` | Coordinates `IService<DimensionIntegrationFormat>.FindAsync()` and `IODataService<DimensionParameters>.FindAll()`, splits format string by delimiter char |
| `IntegratoR.OData.FO/Domain/Models/FinancialDimensions/DimensionFormat.cs` | `required string Delimiter`, `List<string> Segments` |
| `IntegratoR.OData.FO/Features/Commands/LedgerJournals/CreateLedgerJournalHeader/CreateLedgerJournalHeaderCommand.cs` | Inherits `CreateCommand<T>` -- `GetLoggingContext()` comes from base |
| `IntegratoR.OData.FO/Features/Commands/LedgerJournals/CreateLedgerJournalHeader/CreateLedgerJournalHeadersCommand.cs` | Overrides `GetLoggingContext()` with `EntityType`, `Count`, `JournalNames` |
| `IntegratoR.OData.FO/Features/Commands/LedgerJournals/CreateLedgerJournalLine/CreateLedgerJournalLinesCommand.cs` | Overrides `GetLoggingContext()` with `Count`, `JournalNames` (but values are batch numbers) |
| `IntegratoR.OData.FO/Features/Commands/LedgerJournals/UpdateLedgerJournalLine/UpdateLedgerJournalLinesCommand.cs` | Overrides `GetLoggingContext()` with `Count`, `JournalBatchNumbers` (distinct, comma-joined) |
| `IntegratoR.OData.FO/Common/Extensions/ApplicationDependencyInjection.cs` | `AddODataClientFOProxy(IConfiguration)` and `AddODataClientFOProxy(Action<FOSettings>)`, registers MediatR with `RegisterGenericHandlers = true` |
| `IntegratoR.Abstractions/Interfaces/Services/IService.cs` | `AddAsync`, `UpdateAsync`, `FindAsync` -- mocked in handler tests |
| `IntegratoR.OData/Interfaces/Services/IODataBatchService.cs` | `AddBatchAsync`, `UpdateBatchAsync` -- mocked in batch handler tests |
| `IntegratoR.OData/Interfaces/Services/IODataService.cs` | `FindAll` -- mocked in dimension query handler tests |
| `tests/IntegratoR.TestKit/Assertions/ResultAssertionExtensions.cs` | Custom result assertions |

## Edge Cases

- **`LedgerJournalHeader.JournalBatchNumber` null coalescing**: `GetCompositeKey()` returns `"null"` string literal when `JournalBatchNumber` is null. Test must verify the string `"null"`, not actual null.
- **`LedgerJournalLine.LineNumber` is decimal**: The composite key includes a `decimal` value (`1.0m`), not int. Tests must use decimal literals.
- **`FinancialDimensionBuilder.Add` ignores whitespace values too**: The code checks `!string.IsNullOrWhiteSpace(value)` in addition to name. A test with `Add("BU", "  ")` should verify the segment is excluded.
- **`DimensionSegmentDelimiterExtensions` incomplete implementation**: Only `Hyphen` is handled. All other enum values (Period=1 through DoubleTilde=9) throw. Document this as known limitation. Tests should verify at least one non-Hyphen value throws.
- **`GetDimensionOrdersQueryHandler` null reference risk**: If `FindAsync` returns success but with an empty collection, `FirstOrDefault()` returns null, and `null.FinancialDimensionFormat?.Split(...)` returns null, which becomes `Segments = null ?? new List<string>()`. However, `dimensionDelimiter` could also be null/default if no parameters are returned. Test should verify this path.
- **Handler generic type constraints**: All F&O handlers are constrained to `where TEntity : LedgerJournalHeader` or `where TEntity : LedgerJournalLine`. Tests use the base types directly (not custom subclasses).
- **`UpdateLedgerJournalHandler` class naming**: The batch update header handler is named `UpdateLedgerJournalHandler<TEntity>` (not `UpdateLedgerJournalHeadersHandler`). The test class and constructor must reference this exact name.
- **`UpdateLedgerJournalHeaderHandler` uses if/IsFailed pattern**: Unlike Create handlers which use `.Match()`, this handler uses `if (updateResult.IsFailed)` pattern. Both patterns produce the same result but test assertions are identical.
- **`CreateLedgerJournalLinesCommand.GetLoggingContext()` key naming**: The key is `"JournalNames"` but the values are `JournalBatchNumber` (line entity, not header). This is likely intentional for consistency but should be documented in tests.
- **`UpdateLedgerJournalLinesCommand.GetLoggingContext()` uses Distinct()**: The command calls `.Distinct()` on batch numbers. Tests should provide duplicate batch numbers and verify they are deduplicated.

## Expected File Changes

### New Files

| Path | Description |
|---|---|
| `tests/IntegratoR.OData.FO.Tests/Domain/Entities/LedgerJournal/LedgerJournalHeaderTests.cs` | 3 tests for header entity |
| `tests/IntegratoR.OData.FO.Tests/Domain/Entities/LedgerJournal/LedgerJournalLineTests.cs` | 2 tests for line entity |
| `tests/IntegratoR.OData.FO.Tests/Domain/Entities/Dimensions/DimensionEntityTests.cs` | 2 tests for dimension entities |
| `tests/IntegratoR.OData.FO.Tests/Builders/FinancialDimensionBuilderTests.cs` | 8 tests for dimension builder |
| `tests/IntegratoR.OData.FO.Tests/Common/Extensions/DimensionSegmentDelimiterExtensionsTests.cs` | 3 tests for delimiter extensions |
| `tests/IntegratoR.OData.FO.Tests/Common/Extensions/ApplicationDependencyInjectionTests.cs` | 2 tests for DI registration |
| `tests/IntegratoR.OData.FO.Tests/Features/Queries/Dimensions/GetDimensionOrder/GetDimensionOrdersQueryTests.cs` | 3 tests for query record |
| `tests/IntegratoR.OData.FO.Tests/Features/Queries/Dimensions/GetDimensionOrder/GetDimensionOrdersQueryValidatorTests.cs` | 4 tests for validator |
| `tests/IntegratoR.OData.FO.Tests/Features/Queries/Dimensions/GetDimensionOrder/GetDimensionOrdersQueryHandlerTests.cs` | 5 tests for query handler |
| `tests/IntegratoR.OData.FO.Tests/Features/Commands/LedgerJournals/CreateLedgerJournalHeader/CreateLedgerJournalHeaderHandlerTests.cs` | 4 tests (2 single + 2 batch) |
| `tests/IntegratoR.OData.FO.Tests/Features/Commands/LedgerJournals/CreateLedgerJournalLine/CreateLedgerJournalLineHandlerTests.cs` | 4 tests (2 single + 2 batch) |
| `tests/IntegratoR.OData.FO.Tests/Features/Commands/LedgerJournals/UpdateLedgerJournalHeader/UpdateLedgerJournalHeaderHandlerTests.cs` | 4 tests (2 single + 2 batch) |
| `tests/IntegratoR.OData.FO.Tests/Features/Commands/LedgerJournals/UpdateLedgerJournalLine/UpdateLedgerJournalLineHandlerTests.cs` | 4 tests (2 single + 2 batch) |
| `tests/IntegratoR.OData.FO.Tests/Features/Commands/LedgerJournals/CommandLoggingContextTests.cs` | 8 tests for command logging |

### Modified Files

None -- the `.csproj` is handled by the `testkit` mission.

## Done When

1. `dotnet test --filter "FullyQualifiedName~IntegratoR.OData.FO.Tests"` passes with 56 tests green
2. All 15 test classes exist at the expected paths mirroring the source structure
3. Entity tests verify exact `GetCompositeKey()` return values including null-coalesced string literal `"null"` and decimal `LineNumber`
4. `FinancialDimensionBuilderTests` covers all 8 scenarios: uninitialised, all segments, missing middle, out-of-order, single segment, null/whitespace, clear, and reinitialise
5. `DimensionSegmentDelimiterExtensionsTests` covers Hyphen success, unsupported enum throws, and null throws
6. `GetDimensionOrdersQueryTests` verifies cache key format, 15-minute duration, and logging context keys
7. `GetDimensionOrdersQueryValidatorTests` covers valid, empty, over-length, and invalid enum inputs
8. `GetDimensionOrdersQueryHandlerTests` covers success path with correct segment parsing, both service failure paths, and empty results
9. All 8 create handler tests verify success/failure delegation for single and batch operations on both header and line entities
10. All 8 update handler tests verify the same patterns for update operations
11. All 8 command logging context tests verify `GetLoggingContext()` returns expected dictionary keys and values, including batch deduplication
12. DI tests verify MediatR registration with generic handlers and FOSettings binding
13. All tests follow AAA pattern with `MethodName_Scenario_ExpectedResult` naming
14. Custom `ResultAssertions` from TestKit are used for all Result assertions
15. No production code is modified

## TDD Guidance

**Test framework**: xUnit.v3, NSubstitute 5.3.x, FluentAssertions 8.x

**Recommended implementation order** (by complexity):
1. Entity tests (Header, Line, Dimensions) -- no mocks, pure assertions, ~7 tests
2. `FinancialDimensionBuilderTests` -- pure logic, no mocks, 8 tests
3. `DimensionSegmentDelimiterExtensionsTests` -- pure logic, 3 tests
4. `GetDimensionOrdersQueryTests` -- record assertions, 3 tests
5. `GetDimensionOrdersQueryValidatorTests` -- FluentValidation test helpers, 4 tests
6. `CommandLoggingContextTests` -- record instantiation, 8 tests
7. `FODependencyInjectionTests` -- real ServiceCollection, 2 tests
8. Create handler tests -- IService mocking, 8 tests
9. Update handler tests -- same mocking pattern, 8 tests
10. `GetDimensionOrdersQueryHandlerTests` -- most complex mocking, 5 tests

**Key mock patterns**:

```csharp
// Handler test pattern (same for all 8 handlers)
var service = Substitute.For<IService<LedgerJournalHeader>>();
var logger = Substitute.For<ILogger<CreateLedgerJournalHeaderHandler<LedgerJournalHeader>>>();
var handler = new CreateLedgerJournalHeaderHandler<LedgerJournalHeader>(logger, service);

var header = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalName = "GenJnl",
    Description = "Test journal"
};

// Success path
service.AddAsync(header, Arg.Any<CancellationToken>()).Returns(Result.Ok(header));
var result = await handler.Handle(
    new CreateLedgerJournalHeaderCommand<LedgerJournalHeader>(header),
    CancellationToken.None);
result.Should().BeSuccessful().And.HaveValue(header);

// Failure path
var error = new IntegrationError("TestError", "Something failed", ErrorType.Failure);
service.AddAsync(header, Arg.Any<CancellationToken>()).Returns(Result.Fail<LedgerJournalHeader>(error));
var failResult = await handler.Handle(
    new CreateLedgerJournalHeaderCommand<LedgerJournalHeader>(header),
    CancellationToken.None);
failResult.Should().BeFailed().And.HaveErrorCode("TestError");
```

```csharp
// Dimension query handler test setup
var dimParamsService = Substitute.For<IODataService<DimensionParameters>>();
var dimFormatService = Substitute.For<IService<DimensionIntegrationFormat>>();
var logger = Substitute.For<ILogger<GetDimensionOrdersQueryHandler>>();

var dimParams = new DimensionParameters
{
    Key = "Default",
    DimensionSegmentDelimiter = DimensionSegmentDelimiter.Hyphen
};
dimParamsService.FindAll(Arg.Any<CancellationToken>())
    .Returns(Result.Ok<IEnumerable<DimensionParameters>>(new[] { dimParams }));

var dimFormat = new DimensionIntegrationFormat
{
    DimensionFormatName = "DefaultFormat",
    DimensionFormatType = DimensionHierarchyType.DataEntityDefaultDimensionFormat,
    FinancialDimensionFormat = "MainAccount-BU-Dept",
    IsActive = NoYes.Yes
};
dimFormatService.FindAsync(Arg.Any<Expression<Func<DimensionIntegrationFormat, bool>>>(), Arg.Any<CancellationToken>())
    .Returns(Result.Ok<IEnumerable<DimensionIntegrationFormat>>(new[] { dimFormat }));
```

```csharp
// FluentValidation test pattern
var validator = new GetDimensionOrdersQueryValidator();
var query = new GetDimensionOrdersQuery("", DimensionHierarchyType.AccountStructure);
var result = validator.TestValidate(query);
result.ShouldHaveValidationErrorFor(q => q.dimensionFormat);
```

## Reference

See `docs/testing/quest-4-odata-fo.md` for full test matrix.
