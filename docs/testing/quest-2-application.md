# Quest 2: Application (Use Cases Layer)

**Project**: `tests/IntegratoR.Application.Tests/`
**Scope**: Pipeline behaviours, authentication, cache services, generic CQRS handlers, validators. Requires mocking `IService<T>`, `ILogger<T>`, `ICacheService`, `IMemoryCache`, `IDistributedCache`, `IValidator<T>`.
**Total**: 13 missions (+ 8 validator sub-missions), ~92 tests

---

## Mission 2.1: LoggingBehaviourTests [M]

**Source**: `IntegratoR.Application/Common/Behaviours/LoggingBehaviour.cs`
**Mocks**: `ILogger<LoggingBehaviour<TRequest, TResponse>>`

| # | Test Method |
|---|---|
| 1 | `Handle_SuccessfulRequest_LogsStartAndCompletionWithElapsedTime` |
| 2 | `Handle_FailedResult_LogsWarningWithErrorCodeAndMessage` |
| 3 | `Handle_UnhandledException_LogsErrorAndRethrows` |
| 4 | `Handle_Request_CreatesLoggingScopeFromGetLoggingContext` |
| 5 | `Handle_SuccessfulRequest_LogsDebugResponsePayload` |

---

## Mission 2.2: ValidationBehaviourTests [M]

**Source**: `IntegratoR.Application/Common/Behaviours/ValidationBehaviour.cs`
**Mocks**: `IEnumerable<IValidator<TRequest>>`

| # | Test Method |
|---|---|
| 1 | `Handle_NoValidators_PassesThroughToNextHandler` |
| 2 | `Handle_ValidRequest_PassesThroughToNextHandler` |
| 3 | `Handle_InvalidRequest_ShortCircuitsWithValidationError` |
| 4 | `Handle_InvalidRequest_ErrorCodeIsValidationError` |
| 5 | `Handle_InvalidRequest_ErrorTypeIsValidation` |
| 6 | `Handle_GenericResultResponse_CreatesCorrectGenericFailType` |
| 7 | `Handle_NonGenericResultResponse_CreatesCorrectNonGenericFailType` |
| 8 | `Handle_MultipleValidationFailures_UsesFirstFailureOnly` |

---

## Mission 2.3: CachingBehaviourTests [M]

**Source**: `IntegratoR.Application/Common/Behaviours/CachingBehaviour.cs`
**Mocks**: `ICacheService`, `ILogger<CachingBehaviour<TRequest, TResponse>>`

| # | Test Method |
|---|---|
| 1 | `Handle_NonCacheableQuery_PassesThroughWithoutCacheInteraction` |
| 2 | `Handle_CacheHit_ReturnsCachedResponse` |
| 3 | `Handle_CacheMiss_ExecutesHandlerAndCachesSuccessfulResult` |
| 4 | `Handle_CacheMiss_FailedResult_DoesNotCache` |
| 5 | `Handle_CacheHit_LogsDebugCacheHit` |
| 6 | `Handle_CacheMiss_LogsDebugCacheMiss` |
| 7 | `Handle_CacheMiss_SuccessfulResult_SetsCorrectCacheDuration` |

---

## Mission 2.4: OAuthAuthenticatorTests [M]

**Source**: `IntegratoR.Application/Common/Authentication/OAuthAuthenticator.cs`
**Mocks**: `IMemoryCache`

| # | Test Method |
|---|---|
| 1 | `GetAccessTokenAsync_CachedToken_ReturnsCachedValue` |
| 2 | `GetAccessTokenAsync_NoCachedToken_AcquiresNewToken` |
| 3 | `GetAccessTokenAsync_MsalServiceException_ReturnsIntegrationError` |
| 4 | `GetAccessTokenAsync_MsalException_ErrorCodeContainsMsalErrorCode` |
| 5 | `GetAccessTokenAsync_CacheKeyFormat_IncludesClientIdAndResource` |

---

## Mission 2.5: InMemoryCacheServiceTests [M]

**Source**: `IntegratoR.Application/Common/Services/InMemoryCacheService.cs`
**Mocks**: Uses real `MemoryCache` (integration-style)

| # | Test Method |
|---|---|
| 1 | `GetAsync_ExistingKey_ReturnsCachedValue` |
| 2 | `GetAsync_NonExistingKey_ReturnsDefault` |
| 3 | `GetAsync_NullOrEmptyKey_ThrowsArgumentException` |
| 4 | `SetAsync_ValidKeyAndValue_StoresInCache` |
| 5 | `SetAsync_NullKey_ThrowsArgumentException` |
| 6 | `SetAsync_NullValue_ThrowsArgumentNullException` |
| 7 | `SetAsync_CustomExpiration_UsesProvidedExpiration` |
| 8 | `SetAsync_NoExpiration_UsesDefault30Minutes` |
| 9 | `RemoveAsync_ExistingKey_RemovesFromCache` |
| 10 | `RemoveAsync_NullKey_ThrowsArgumentException` |

---

## Mission 2.6: DistributedCacheServiceTests [M]

**Source**: `IntegratoR.Application/Common/Services/DistributedCacheService.cs`
**Mocks**: `IDistributedCache`

| # | Test Method |
|---|---|
| 1 | `GetAsync_ExistingKey_DeserializesAndReturnsValue` |
| 2 | `GetAsync_NonExistingKey_ReturnsDefault` |
| 3 | `GetAsync_NullKey_ThrowsArgumentException` |
| 4 | `GetAsync_EmptyBytes_ReturnsDefault` |
| 5 | `SetAsync_ValidKeyAndValue_SerializesToUtf8BytesWithCamelCase` |
| 6 | `SetAsync_NullKey_ThrowsArgumentException` |
| 7 | `SetAsync_NullValue_ThrowsArgumentNullException` |
| 8 | `SetAsync_CustomExpiration_PassesCorrectOptions` |
| 9 | `SetAsync_NoExpiration_UsesDefault30Minutes` |
| 10 | `RemoveAsync_ValidKey_CallsDistributedCacheRemove` |

---

## Mission 2.7: CreateCommandHandlerTests [S]

**Source**: `IntegratoR.Application/Features/Common/Commands/CreateCommandHandler.cs`
**Mocks**: `IService<TestEntity>`

| # | Test Method |
|---|---|
| 1 | `Handle_ValidCommand_DelegatesToServiceAddAsync` |
| 2 | `Handle_ServiceReturnsSuccess_ReturnsSuccessResult` |
| 3 | `Handle_ServiceReturnsFailure_PropagatesErrors` |

---

## Mission 2.8: UpdateCommandHandlerTests [S]

**Source**: `IntegratoR.Application/Features/Common/Commands/UpdateCommandHandler.cs`
**Mocks**: `IService<TestEntity>`

| # | Test Method |
|---|---|
| 1 | `Handle_ValidCommand_DelegatesToServiceUpdateAsync` |
| 2 | `Handle_ServiceReturnsSuccess_ReturnsSuccessResult` |
| 3 | `Handle_ServiceReturnsFailure_PropagatesErrors` |

---

## Mission 2.9: DeleteCommandHandlerTests [S]

**Source**: `IntegratoR.Application/Features/Common/Commands/DeleteCommandHandler.cs`
**Mocks**: `IService<TestEntity>`

| # | Test Method |
|---|---|
| 1 | `Handle_ServiceDeleteSucceeds_ReturnsOkWithOriginalEntity` |
| 2 | `Handle_ServiceDeleteFails_PropagatesErrors` |
| 3 | `Handle_SuccessResult_ConvertsNonGenericToGenericResultWithEntity` |

---

## Mission 2.10: GetByKeyQueryHandlerTests [S]

**Source**: `IntegratoR.Application/Features/Common/Queries/GetByKeyQueryHandler.cs`
**Mocks**: `IService<TestEntity>`, `ILogger<GetByKeyQueryHandler<TestEntity>>`

| # | Test Method |
|---|---|
| 1 | `Handle_EntityFound_ReturnsSuccessWithEntity` |
| 2 | `Handle_ServiceFailure_PropagatesErrors` |
| 3 | `Handle_UsesMatchExtensionForPatternMatching` |

---

## Mission 2.11: GetByFilterQueryHandlerTests [S]

**Source**: `IntegratoR.Application/Features/Common/Queries/GetByFilterQueryHandler.cs`
**Mocks**: `IService<TestEntity>`, `ILogger<GetByFilterQueryHandler<TestEntity>>`

| # | Test Method |
|---|---|
| 1 | `Handle_EntitiesFound_ReturnsSuccessWithMaterializedList` |
| 2 | `Handle_ServiceFailure_PropagatesErrors` |
| 3 | `Handle_EmptyResult_ReturnsSuccessWithEmptyCollection` |

---

## Mission 2.12: ValidatorTests (all 8 validators) [S]

**Source**: `IntegratoR.Application/Features/Common/Validators/` (8 validator classes)
**Mocks**: None -- direct instantiation with FluentValidation's `Validate()`

**One test class per validator:**

| Validator | Tests |
|---|---|
| `CreateCommandValidator<T>` | `Validate_ValidEntity_NoErrors`, `Validate_NullEntity_HasError` |
| `CreateBatchCommandValidator<T>` | `Validate_ValidEntities_NoErrors`, `Validate_NullEntities_HasError`, `Validate_EmptyEntities_HasError` |
| `UpdateCommandValidator<T>` | `Validate_ValidEntity_NoErrors`, `Validate_NullEntity_HasError` |
| `UpdateBatchCommandValidator<T>` | `Validate_ValidEntities_NoErrors`, `Validate_NullEntities_HasError`, `Validate_EmptyEntities_HasError` |
| `DeleteCommandValidator<T>` | `Validate_ValidEntity_NoErrors`, `Validate_NullEntity_HasError` |
| `DeleteBatchCommandValidator<T>` | `Validate_ValidEntities_NoErrors`, `Validate_NullEntities_HasError`, `Validate_EmptyEntities_HasError` |
| `GetByKeyQueryValidator<T>` | `Validate_ValidKey_NoErrors`, `Validate_NullKey_HasError`, `Validate_EmptyKey_HasError`, `Validate_KeyWithNullElement_HasError` |
| `GetByFilterQueryValidator<T>` | `Validate_ValidFilter_NoErrors`, `Validate_NullFilter_HasError` |

---

## Mission 2.13: ApplicationDependencyInjectionTests [S]

**Source**: `IntegratoR.Application/Common/Extensions/ApplicationDependencyInjection.cs`
**Mocks**: None -- real `ServiceCollection`

| # | Test Method |
|---|---|
| 1 | `AddApplicationServices_RegistersPipelineBehaviours_InCorrectOrder` (Logging -> Validation -> Caching) |
| 2 | `AddApplicationServices_RegistersCacheService_AsSingleton` |
| 3 | `AddApplicationServices_RegistersAuthenticator_AsSingleton` |
| 4 | `AddApplicationServices_RegistersMediatR_WithGenericHandlers` |
| 5 | `AddApplicationServices_RegistersValidators_FromAssembly` |
