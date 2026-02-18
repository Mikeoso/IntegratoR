# Quest 1: Abstractions (Innermost Layer)

**Project**: `tests/IntegratoR.Abstractions.Tests/`
**Scope**: Pure unit tests, zero external dependencies. Domain primitives, CQRS records, Result pattern, JSON serialization.
**Total**: 8 missions, ~51 tests

---

## Mission 1.1: BaseEntityTests [S]

**Source**: `IntegratoR.Abstractions/Domain/Entities/BaseEntity.cs`
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `GetLoggingContext_AllPropertiesPopulated_ReturnsDictionaryWithAllPublicProperties` |
| 2 | `GetLoggingContext_NullPropertyValue_ReplacesWithNewObject` |
| 3 | `GetLoggingContext_IndexedProperty_ExcludesIndexedProperties` |
| 4 | `GetLoggingContext_NoPublicProperties_ReturnsEmptyDictionary` |
| 5 | `GetCompositeKey_CompositeKeyEntity_ReturnsCorrectKeyArray` |

---

## Mission 1.2: IntegrationErrorTests [S]

**Source**: `IntegratoR.Abstractions/Common/Results/IntegrationError.cs`
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `Constructor_WithAllParameters_SetsCodeTypeAndMessage` |
| 2 | `Constructor_WithException_SetsCausedByAndExceptionProperty` |
| 3 | `Constructor_WithoutException_ExceptionIsNull` |
| 4 | `Constructor_AllErrorTypes_SetsCorrectType` (Theory: Failure, Validation, NotFound, Conflict) |

---

## Mission 1.3: ResultExtensionsTests [M]

**Source**: `IntegratoR.Abstractions/Common/Results/ResultExtensions.cs`
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `GetError_ResultWithIntegrationError_ReturnsFirstIntegrationError` |
| 2 | `GetError_ResultWithMultipleErrors_ReturnsFirst` |
| 3 | `GetError_ResultWithNonIntegrationErrors_ReturnsNull` |
| 4 | `GetError_SuccessResult_ReturnsNull` |
| 5 | `Match_GenericSuccess_InvokesOnSuccess` |
| 6 | `Match_GenericFailure_WithIntegrationError_InvokesOnFailure` |
| 7 | `Match_GenericFailure_WithoutIntegrationError_CreatesSyntheticError` |
| 8 | `Match_NonGenericSuccess_InvokesOnSuccess` |
| 9 | `Match_NonGenericFailure_WithIntegrationError_InvokesOnFailure` |
| 10 | `Match_NonGenericFailure_WithoutIntegrationError_CreatesSyntheticError` |

---

## Mission 1.4: ResultJsonConverterTests [M]

**Source**: `IntegratoR.Abstractions/Common/Results/ResultJsonConverter.cs` (non-generic `ResultJsonConverter`)
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `WriteJson_SuccessResult_WritesIsSuccessTrueAndEmptyErrors` |
| 2 | `WriteJson_FailedResultWithIntegrationError_WritesCodeMessageType` |
| 3 | `WriteJson_FailedResultWithPlainError_WritesUnknownCodeAndFailureType` |
| 4 | `ReadJson_SuccessJson_ReturnsOkResult` |
| 5 | `ReadJson_FailedJson_ReturnsFailedResultWithIntegrationErrors` |
| 6 | `RoundTrip_SuccessResult_PreservesIsSuccess` |
| 7 | `RoundTrip_FailedResult_PreservesErrorDetails` |

---

## Mission 1.5: ResultJsonConverterGenericTests [M]

**Source**: `IntegratoR.Abstractions/Common/Results/ResultJsonConverter.cs` (generic `ResultJsonConverter<T>`)
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `WriteJson_SuccessResultWithValue_WritesIsSuccessValueAndEmptyErrors` |
| 2 | `WriteJson_FailedResult_WritesIsSuccessFalseAndErrors` |
| 3 | `ReadJson_SuccessJsonWithValue_ReturnsOkResultWithValue` |
| 4 | `ReadJson_FailedJson_ReturnsFailedResult` |
| 5 | `RoundTrip_ComplexType_PreservesValue` |
| 6 | `RoundTrip_FailedResult_PreservesErrors` |

---

## Mission 1.6: ResultGenericJsonConverterTests [M]

**Source**: `IntegratoR.Abstractions/Common/Results/ResultJsonConverter.cs` (`ResultGenericJsonConverter`)
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `CanConvert_ResultOfT_ReturnsTrue` |
| 2 | `CanConvert_NonGenericResult_ReturnsFalse` |
| 3 | `CanConvert_UnrelatedType_ReturnsFalse` |
| 4 | `RoundTrip_ResultOfString_PreservesValue` |
| 5 | `RoundTrip_ResultOfInt_PreservesValue` |
| 6 | `RoundTrip_ResultOfComplexObject_PreservesValue` |
| 7 | `RoundTrip_FailedResultOfT_PreservesErrors` |

---

## Mission 1.7: CqrsCommandRecordTests [S]

**Source**: `IntegratoR.Abstractions/Common/CQRS/Commands/` (all 6 command records)
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `CreateCommand_GetLoggingContext_DelegatesToEntity` |
| 2 | `CreateBatchCommand_GetLoggingContext_ReturnsCountOfEntities` |
| 3 | `UpdateCommand_GetLoggingContext_DelegatesToEntity` |
| 4 | `UpdateBatchCommand_GetLoggingContext_ReturnsCountOfEntities` |
| 5 | `DeleteCommand_GetLoggingContext_DelegatesToEntity` |
| 6 | `DeleteBatchCommand_GetLoggingContext_ReturnsCountOfEntities` |

---

## Mission 1.8: CqrsQueryRecordTests [S]

**Source**: `IntegratoR.Abstractions/Common/CQRS/Queries/` (both query records)
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `GetByKeyQuery_GetLoggingContext_ReturnsEntityTypeAndSerializedKeys` |
| 2 | `GetByFilterQuery_GetLoggingContext_ReturnsEntityTypeAndFilterString` |
