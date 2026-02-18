# Quest 4: OData.FO (D365 Finance & Operations)

**Project**: `tests/IntegratoR.OData.FO.Tests/`
**Scope**: Entity composite keys, financial dimension builder, dimension query/handler, F&O-specific CQRS handlers and commands.
**Total**: 12 missions, ~56 tests

---

## Mission 4.1: LedgerJournalHeaderTests [S]

**Source**: `IntegratoR.OData.FO/Domain/Entities/LedgerJournal/LedgerJournalHeader.cs`
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `GetCompositeKey_AllFieldsSet_ReturnsDataAreaIdAndJournalBatchNumber` |
| 2 | `GetCompositeKey_NullJournalBatchNumber_ReturnsNullStringLiteral` |
| 3 | `GetLoggingContext_ReturnsAllPublicProperties` |

---

## Mission 4.2: LedgerJournalLineTests [S]

**Source**: `IntegratoR.OData.FO/Domain/Entities/LedgerJournal/LedgerJournalLine.cs`
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `GetCompositeKey_AllFieldsSet_ReturnsThreePartKey` |
| 2 | `GetLoggingContext_ReturnsAllPublicProperties` |

---

## Mission 4.3: DimensionEntityTests [S]

**Source**: `IntegratoR.OData.FO/Domain/Entities/Dimensions/` (both entity classes)
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `DimensionIntegrationFormat_GetCompositeKey_ReturnsFormatNameAndType` |
| 2 | `DimensionParameters_GetCompositeKey_ReturnsKey` |

---

## Mission 4.4: FinancialDimensionBuilderTests [M]

**Source**: `IntegratoR.OData.FO/Builders/FinancialDimensionBuilder.cs`
**Mocks**: None (pure logic)

| # | Test Method |
|---|---|
| 1 | `Build_NotInitialized_ReturnsEmptyString` |
| 2 | `Build_AllSegmentsProvided_JoinsWithDelimiter` |
| 3 | `Build_MissingMiddleSegment_InsertsEmptyPlaceholder` |
| 4 | `Build_AddedOutOfOrder_RespectsFormatOrder` |
| 5 | `Build_SingleSegment_NoDelimiter` |
| 6 | `Add_NullOrWhitespaceName_IgnoresEntry` |
| 7 | `Clear_AfterAdditions_ResetsState` |
| 8 | `Initialize_AfterPreviousUse_ClearsState` |

---

## Mission 4.5: DimensionSegmentDelimiterExtensionsTests [S]

**Source**: `IntegratoR.OData.FO/Common/Extensions/DimensionSegmentDelimiterExtensions.cs`
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `GetCharValue_Hyphen_ReturnsHyphenChar` |
| 2 | `GetCharValue_UnsupportedEnum_ThrowsArgumentOutOfRangeException` |
| 3 | `GetCharValue_Null_ThrowsArgumentOutOfRangeException` |

---

## Mission 4.6: GetDimensionOrdersQueryTests [S]

**Source**: `IntegratoR.OData.FO/Features/Queries/Dimensions/GetDimensionOrder/GetDimensionOrdersQuery.cs`
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `CacheKey_ContainsQueryNameFormatAndHierarchyType` |
| 2 | `CacheDuration_Is15Minutes` |
| 3 | `GetLoggingContext_ContainsDimensionFormatAndHierarchyType` |

---

## Mission 4.7: GetDimensionOrdersQueryHandlerTests [M]

**Source**: `IntegratoR.OData.FO/Features/Queries/Dimensions/GetDimensionOrder/GetDimensionOrdersQueryHandler.cs`
**Mocks**: `ILogger`, `IODataService<DimensionParameters>`, `IService<DimensionIntegrationFormat>`

| # | Test Method |
|---|---|
| 1 | `Handle_ValidRequest_ReturnsDimensionFormatWithSegmentsAndDelimiter` |
| 2 | `Handle_DimensionFormatsQueryFails_ReturnsFailure` |
| 3 | `Handle_DimensionParametersQueryFails_ReturnsFailure` |
| 4 | `Handle_NoDimensionFormats_ReturnsError` |
| 5 | `Handle_ParsesFinancialDimensionFormatString_IntoSegments` |

---

## Mission 4.8: GetDimensionOrdersQueryValidatorTests [S]

**Source**: `IntegratoR.OData.FO/Features/Queries/Dimensions/GetDimensionOrder/GetDimensionOrdersQueryValidator.cs`
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `Validate_ValidQuery_HasNoErrors` |
| 2 | `Validate_EmptyDimensionFormat_HasError` |
| 3 | `Validate_DimensionFormatExceeds100Chars_HasError` |
| 4 | `Validate_InvalidHierarchyType_HasError` |

---

## Mission 4.9: FOCreateHandlerTests [M]

**Source**: `IntegratoR.OData.FO/Features/Commands/LedgerJournals/Create*/` (4 handlers)
**Mocks**: `IService<T>`, `IODataBatchService<T>`, `ILogger<T>`

| Handler | Tests |
|---|---|
| `CreateLedgerJournalHeaderHandler` | `Handle_Success_ReturnsOkWithEntity`, `Handle_Failure_ReturnsError` |
| `CreateLedgerJournalHeadersHandler` (batch) | `Handle_Success_ReturnsOk`, `Handle_Failure_ReturnsError` |
| `CreateLedgerJournalLineHandler` | `Handle_Success_ReturnsOkWithEntity`, `Handle_Failure_ReturnsError` |
| `CreateLedgerJournalLinesHandler` (batch) | `Handle_Success_ReturnsOk`, `Handle_Failure_ReturnsError` |

---

## Mission 4.10: FOUpdateHandlerTests [M]

**Source**: `IntegratoR.OData.FO/Features/Commands/LedgerJournals/Update*/` (4 handlers)
**Mocks**: `IService<T>`, `IODataBatchService<T>`, `ILogger<T>`

| Handler | Tests |
|---|---|
| `UpdateLedgerJournalHeaderHandler` | `Handle_Success_ReturnsOkWithEntity`, `Handle_Failure_ReturnsError` |
| `UpdateLedgerJournalHeadersHandler` (batch) | `Handle_Success_ReturnsOk`, `Handle_Failure_ReturnsError` |
| `UpdateLedgerJournalLineHandler` | `Handle_Success_ReturnsOkWithEntity`, `Handle_Failure_ReturnsError` |
| `UpdateLedgerJournalLinesHandler` (batch) | `Handle_Success_ReturnsOk`, `Handle_Failure_ReturnsError` |

---

## Mission 4.11: FOCommandLoggingContextTests [S]

**Source**: All F&O command record files
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `CreateLedgerJournalHeaderCommand_GetLoggingContext_DelegatesToEntity` |
| 2 | `CreateLedgerJournalHeadersCommand_GetLoggingContext_ReturnsCountAndJournalNames` |
| 3 | `CreateLedgerJournalLineCommand_GetLoggingContext_DelegatesToEntity` |
| 4 | `CreateLedgerJournalLinesCommand_GetLoggingContext_ReturnsCountAndBatchNumbers` |
| 5 | `UpdateLedgerJournalHeaderCommand_GetLoggingContext_DelegatesToEntity` |
| 6 | `UpdateLedgerJournalHeadersCommand_GetLoggingContext_ReturnsCountAndJournalNames` |
| 7 | `UpdateLedgerJournalLineCommand_GetLoggingContext_DelegatesToEntity` |
| 8 | `UpdateLedgerJournalLinesCommand_GetLoggingContext_ReturnsCountAndBatchNumbers` |

---

## Mission 4.12: FODependencyInjectionTests [S]

**Source**: `IntegratoR.OData.FO/Common/Extensions/ApplicationDependencyInjection.cs`
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `AddODataClientFOProxy_RegistersMediatR_WithGenericHandlers` |
| 2 | `AddODataClientFOProxy_ConfiguresFOSettings` |
