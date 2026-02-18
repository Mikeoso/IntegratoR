---
codename: "application"
title: "Application Tests"
quest: "test-suite"
status: planned
complexity: "L"
depends_on: ["testkit"]
created: "2026-02-18"
updated: "2026-02-18"
---

## Objective

Create `tests/IntegratoR.Application.Tests/` with comprehensive unit tests for the use cases layer. This covers 13 test classes totalling ~92 tests across pipeline behaviours (Logging, Validation, Caching), authentication (OAuthAuthenticator), cache services (InMemory + Distributed), generic CQRS handlers (Create/Update/Delete commands, GetByKey/GetByFilter queries), all 8 validators, and DI registration.

## Approach

### 1. Project structure

The `.csproj` shell should already exist from the `testkit` mission. Add references to `IntegratoR.Application` and `IntegratoR.TestKit`. Mirror source folder structure:

```
tests/IntegratoR.Application.Tests/
  Common/Behaviours/
    LoggingBehaviourTests.cs
    ValidationBehaviourTests.cs
    CachingBehaviourTests.cs
  Common/Authentication/
    OAuthAuthenticatorTests.cs
  Common/Services/
    InMemoryCacheServiceTests.cs
    DistributedCacheServiceTests.cs
  Common/Extensions/
    ApplicationDependencyInjectionTests.cs
  Features/Common/Commands/
    CreateCommandHandlerTests.cs
    UpdateCommandHandlerTests.cs
    DeleteCommandHandlerTests.cs
  Features/Common/Queries/
    GetByKeyQueryHandlerTests.cs
    GetByFilterQueryHandlerTests.cs
  Features/Common/Validators/
    ValidatorTests.cs (or 8 separate files)
```

### 2. LoggingBehaviourTests (5 tests)

**Mocks**: `ILogger<LoggingBehaviour<TRequest, TResponse>>` (NSubstitute)

The `LoggingBehaviour` requires `TRequest : IRequest<TResponse>, IContext`. Use a test request type that implements both. Use `TestCacheableQuery<T>` from TestKit or create a local test request.

| Test | What to Assert |
|---|---|
| `Handle_SuccessfulRequest_LogsStartAndCompletionWithElapsedTime` | `LogInformation` called twice (start + completion) |
| `Handle_FailedResult_LogsWarningWithErrorCodeAndMessage` | `LogWarning` called with error details when `Result.Fail` returned |
| `Handle_UnhandledException_LogsErrorAndRethrows` | `LogError` called with exception; exception re-thrown |
| `Handle_Request_CreatesLoggingScopeFromGetLoggingContext` | `BeginScope` called with dictionary from `GetLoggingContext()` |
| `Handle_SuccessfulRequest_LogsDebugResponsePayload` | `LogDebug` called with response payload |

**Note**: xUnit.v3 + NSubstitute logger verification uses `_logger.ReceivedCalls()` or a custom log-capturing approach since `ILogger` extension methods are static. Consider using a test logger or verifying `_logger.ReceivedWithAnyArgs().Log(...)`.

### 3. ValidationBehaviourTests (8 tests)

**Mocks**: `IEnumerable<IValidator<TRequest>>` (NSubstitute)

The `ValidationBehaviour` requires `TResponse : IResultBase`. Create requests returning `Result<TestEntity>` or `Result`.

| Test | What to Assert |
|---|---|
| `Handle_NoValidators_PassesThroughToNextHandler` | `next()` delegate invoked; result returned |
| `Handle_ValidRequest_PassesThroughToNextHandler` | Validator returns no errors; `next()` invoked |
| `Handle_InvalidRequest_ShortCircuitsWithValidationError` | `next()` NOT invoked; result is failed |
| `Handle_InvalidRequest_ErrorCodeIsValidationError` | Error code is `"Validation.Error"` |
| `Handle_InvalidRequest_ErrorTypeIsValidation` | Error type is `ErrorType.Validation` |
| `Handle_GenericResultResponse_CreatesCorrectGenericFailType` | For `Result<TestEntity>`, returns `Result<TestEntity>` (not cast error) |
| `Handle_NonGenericResultResponse_CreatesCorrectNonGenericFailType` | For `Result`, returns `Result` |
| `Handle_MultipleValidationFailures_UsesFirstFailureOnly` | Only first validation error message appears |

**Key**: The `ValidationBehaviour` uses reflection to create `Result.Fail<T>()` for generic `Result<T>` responses. Test both generic and non-generic `TResponse` types to validate the reflection path.

### 4. CachingBehaviourTests (7 tests)

**Mocks**: `ICacheService` (NSubstitute), `ILogger<CachingBehaviour<TRequest, TResponse>>`

Use `TestCacheableQuery<T>` from TestKit and `FakeCacheService` from TestKit.

| Test | What to Assert |
|---|---|
| `Handle_NonCacheableQuery_PassesThroughWithoutCacheInteraction` | `_cacheService` not called; `next()` invoked |
| `Handle_CacheHit_ReturnsCachedResponse` | `GetAsync` returns value; `next()` NOT invoked |
| `Handle_CacheMiss_ExecutesHandlerAndCachesSuccessfulResult` | `GetAsync` returns null; `next()` invoked; `SetAsync` called |
| `Handle_CacheMiss_FailedResult_DoesNotCache` | Handler returns failed result; `SetAsync` NOT called |
| `Handle_CacheHit_LogsDebugCacheHit` | Logger receives cache hit message |
| `Handle_CacheMiss_LogsDebugCacheMiss` | Logger receives cache miss message |
| `Handle_CacheMiss_SuccessfulResult_SetsCorrectCacheDuration` | `SetAsync` called with correct `CacheDuration` from query |

### 5. OAuthAuthenticatorTests (5 tests)

**Mocks**: `IMemoryCache` (NSubstitute)

The `OAuthAuthenticator` uses MSAL `ConfidentialClientApplicationBuilder` internally. Only the cache-hit path is easily unit-testable. The MSAL token acquisition path requires either integration testing or mocking MSAL internals (which is not recommended).

| Test | What to Assert |
|---|---|
| `GetAccessTokenAsync_CachedToken_ReturnsCachedValue` | `TryGetValue` returns true with cached token; result is success |
| `GetAccessTokenAsync_NoCachedToken_AcquiresNewToken` | This test may require MSAL integration -- mark as integration test or skip |
| `GetAccessTokenAsync_MsalServiceException_ReturnsIntegrationError` | Difficult to unit test without MSAL mocking |
| `GetAccessTokenAsync_MsalException_ErrorCodeContainsMsalErrorCode` | Same constraint |
| `GetAccessTokenAsync_CacheKeyFormat_IncludesClientIdAndResource` | Verify `TryGetValue` called with `"AccessToken-{clientId}-{resource}"` |

**Pragmatic approach**: Focus on the cache-hit and cache-key-format tests. For MSAL integration, either use `[Fact(Skip = "...")]` or restructure to extract MSAL interaction behind an abstraction.

### 6. InMemoryCacheServiceTests (10 tests)

**Uses real `MemoryCache`** (integration-style, not mocked).

| Test | What to Assert |
|---|---|
| `GetAsync_ExistingKey_ReturnsCachedValue` | Set then get returns the value |
| `GetAsync_NonExistingKey_ReturnsDefault` | Returns `default(T)` |
| `GetAsync_NullOrEmptyKey_ThrowsArgumentException` | `ArgumentNullException` thrown |
| `SetAsync_ValidKeyAndValue_StoresInCache` | Get after set returns value |
| `SetAsync_NullKey_ThrowsArgumentException` | `ArgumentNullException` thrown |
| `SetAsync_NullValue_ThrowsArgumentNullException` | `ArgumentNullException` thrown |
| `SetAsync_CustomExpiration_UsesProvidedExpiration` | Value expires after custom duration |
| `SetAsync_NoExpiration_UsesDefault30Minutes` | Entry created with 30min AbsoluteExpirationRelativeToNow |
| `RemoveAsync_ExistingKey_RemovesFromCache` | Get after remove returns default |
| `RemoveAsync_NullKey_ThrowsArgumentException` | `ArgumentNullException` thrown |

Create `new MemoryCache(new MemoryCacheOptions())` in test setup.

### 7. DistributedCacheServiceTests (10 tests)

**Mocks**: `IDistributedCache` (NSubstitute)

| Test | What to Assert |
|---|---|
| `GetAsync_ExistingKey_DeserializesAndReturnsValue` | `GetAsync` returns UTF-8 JSON bytes; result is deserialized object |
| `GetAsync_NonExistingKey_ReturnsDefault` | `GetAsync` returns null; result is `default` |
| `GetAsync_NullKey_ThrowsArgumentException` | `ArgumentException` thrown |
| `GetAsync_EmptyBytes_ReturnsDefault` | `GetAsync` returns empty byte array; result is `default` |
| `SetAsync_ValidKeyAndValue_SerializesToUtf8BytesWithCamelCase` | Capture bytes passed to `SetAsync`; deserialize and verify camelCase |
| `SetAsync_NullKey_ThrowsArgumentException` | `ArgumentException` thrown |
| `SetAsync_NullValue_ThrowsArgumentNullException` | `ArgumentNullException` thrown |
| `SetAsync_CustomExpiration_PassesCorrectOptions` | Capture `DistributedCacheEntryOptions`; verify `AbsoluteExpirationRelativeToNow` |
| `SetAsync_NoExpiration_UsesDefault30Minutes` | Options have 30min absolute expiration |
| `RemoveAsync_ValidKey_CallsDistributedCacheRemove` | `RemoveAsync` received 1 call |

### 8. CreateCommandHandlerTests (3 tests)

**Mocks**: `IService<TestEntity>` (NSubstitute)

- `Handle_ValidCommand_DelegatesToServiceAddAsync` -- `_service.AddAsync` received 1 call with correct entity
- `Handle_ServiceReturnsSuccess_ReturnsSuccessResult` -- Service returns `Result.Ok(entity)`; handler returns success
- `Handle_ServiceReturnsFailure_PropagatesErrors` -- Service returns `Result.Fail`; handler propagates errors

### 9. UpdateCommandHandlerTests (3 tests)

Same pattern as Create. Delegates to `_service.UpdateAsync`.

### 10. DeleteCommandHandlerTests (3 tests)

**Key difference**: Delete handler converts non-generic `Result` success to `Result.Ok(request.Entity)` and propagates errors from `Result.Fail<TEntity>(result.Errors)`.

- `Handle_ServiceDeleteSucceeds_ReturnsOkWithOriginalEntity`
- `Handle_ServiceDeleteFails_PropagatesErrors`
- `Handle_SuccessResult_ConvertsNonGenericToGenericResultWithEntity`

### 11. GetByKeyQueryHandlerTests (3 tests)

**Mocks**: `IService<TestEntity>`, `ILogger<GetByKeyQueryHandler<TestEntity>>`

- `Handle_EntityFound_ReturnsSuccessWithEntity`
- `Handle_ServiceFailure_PropagatesErrors`
- `Handle_UsesMatchExtensionForPatternMatching` -- Verify the handler uses `Match()` (result flows correctly)

### 12. GetByFilterQueryHandlerTests (3 tests)

**Mocks**: `IService<TestEntity>`, `ILogger<GetByFilterQueryHandler<TestEntity>>`

- `Handle_EntitiesFound_ReturnsSuccessWithMaterializedList`
- `Handle_ServiceFailure_PropagatesErrors`
- `Handle_EmptyResult_ReturnsSuccessWithEmptyCollection`

### 13. ValidatorTests (8 validator classes, ~22 tests total)

All validators use direct instantiation with FluentValidation's `Validate()`. No mocking needed.

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

### 14. ApplicationDependencyInjectionTests (5 tests)

**No mocks** -- use real `ServiceCollection`.

- `AddApplicationServices_RegistersPipelineBehaviours_InCorrectOrder` -- Logging -> Validation -> Caching
- `AddApplicationServices_RegistersCacheService_AsSingleton`
- `AddApplicationServices_RegistersAuthenticator_AsSingleton`
- `AddApplicationServices_RegistersMediatR_WithGenericHandlers`
- `AddApplicationServices_RegistersValidators_FromAssembly`

## Existing Code to Leverage

| Component | Path |
|---|---|
| LoggingBehaviour | `IntegratoR.Application/Common/Behaviours/LoggingBehaviour.cs` |
| ValidationBehaviour | `IntegratoR.Application/Common/Behaviours/ValidationBehaviour.cs` |
| CachingBehaviour | `IntegratoR.Application/Common/Behaviours/CachingBehaviour.cs` |
| OAuthAuthenticator | `IntegratoR.Application/Common/Authentication/OAuthAuthenticator.cs` |
| InMemoryCacheService | `IntegratoR.Application/Common/Services/InMemoryCacheService.cs` |
| DistributedCacheService | `IntegratoR.Application/Common/Services/DistributedCacheService.cs` |
| All generic handlers | `IntegratoR.Application/Features/Common/Commands/*.cs`, `Queries/*.cs` |
| All validators | `IntegratoR.Application/Features/Common/Validators/*.cs` |
| DI registration | `IntegratoR.Application/Common/Extensions/ApplicationDependencyInjection.cs` |
| TestEntity, TestEntityBuilder, FakeCacheService, TestCacheableQuery | `tests/IntegratoR.TestKit/` |
| Custom Result assertions | `tests/IntegratoR.TestKit/Assertions/` |

## Edge Cases

- **LoggingBehaviour**: `ILogger` extension methods are static; NSubstitute cannot intercept them directly. Use `_logger.ReceivedWithAnyArgs().Log(...)` or a log-capturing test helper.
- **ValidationBehaviour**: The reflection path for creating `Result.Fail<T>()` differs for generic vs. non-generic `Result`. Both paths must be tested.
- **CachingBehaviour**: Non-cacheable requests (not implementing `ICacheableQuery<T>`) must pass through without any cache interaction.
- **InMemoryCacheService**: Thread safety via `SemaphoreSlim` -- testing concurrent access is not required but guard against deadlocks in test setup.
- **DistributedCacheService**: Uses `System.Text.Json` with `CamelCase` naming policy for serialization. Verify bytes round-trip correctly.
- **DeleteCommandHandler**: Converts non-generic `Result` from `_service.DeleteAsync` to `Result<TEntity>` -- test the conversion logic.
- **Batch validators**: Test with `null` entities collection AND empty collection as separate cases.
- **DI registration order**: Pipeline behaviours are registered as open generics in specific order. Verify the order by inspecting `ServiceDescriptor` instances in the collection.

## Expected File Changes

| Action | File Path |
|--------|-----------|
| CREATE | `tests/IntegratoR.Application.Tests/Common/Behaviours/LoggingBehaviourTests.cs` |
| CREATE | `tests/IntegratoR.Application.Tests/Common/Behaviours/ValidationBehaviourTests.cs` |
| CREATE | `tests/IntegratoR.Application.Tests/Common/Behaviours/CachingBehaviourTests.cs` |
| CREATE | `tests/IntegratoR.Application.Tests/Common/Authentication/OAuthAuthenticatorTests.cs` |
| CREATE | `tests/IntegratoR.Application.Tests/Common/Services/InMemoryCacheServiceTests.cs` |
| CREATE | `tests/IntegratoR.Application.Tests/Common/Services/DistributedCacheServiceTests.cs` |
| CREATE | `tests/IntegratoR.Application.Tests/Common/Extensions/ApplicationDependencyInjectionTests.cs` |
| CREATE | `tests/IntegratoR.Application.Tests/Features/Common/Commands/CreateCommandHandlerTests.cs` |
| CREATE | `tests/IntegratoR.Application.Tests/Features/Common/Commands/UpdateCommandHandlerTests.cs` |
| CREATE | `tests/IntegratoR.Application.Tests/Features/Common/Commands/DeleteCommandHandlerTests.cs` |
| CREATE | `tests/IntegratoR.Application.Tests/Features/Common/Queries/GetByKeyQueryHandlerTests.cs` |
| CREATE | `tests/IntegratoR.Application.Tests/Features/Common/Queries/GetByFilterQueryHandlerTests.cs` |
| CREATE | `tests/IntegratoR.Application.Tests/Features/Common/Validators/ValidatorTests.cs` |

## Done When

1. All 13+ test classes exist mirroring the source project folder structure
2. All ~92 tests pass via `dotnet test`
3. Pipeline behaviour tests cover: logging scopes, validation short-circuit, caching hit/miss/skip
4. Cache service tests cover: both InMemory and Distributed implementations with argument validation
5. Handler tests cover: success delegation, error propagation, and DeleteHandler's Result conversion
6. All 8 validators tested with valid and invalid inputs
7. DI test verifies behaviour registration order, service lifetimes, and assembly scanning
8. Tests follow AAA pattern, British spelling ("Behaviour"), `MethodName_Scenario_ExpectedResult` naming
9. TestKit entities and assertions used throughout -- not production D365 entities

## TDD Guidance

**Test framework**: xUnit.v3 with `[Fact]` and `[Theory]`/`[InlineData]`
**Assertions**: FluentAssertions 8.x, custom `ResultAssertions` from TestKit
**Mocking**: NSubstitute 5.3.x for `IService<T>`, `ILogger<T>`, `ICacheService`, `IMemoryCache`, `IDistributedCache`, `IValidator<T>`

**Key patterns**:
- For behaviour tests, create a `RequestHandlerDelegate<TResponse>` as a lambda returning a preset result.
- For ValidationBehaviour, create an `IValidator<T>` mock that returns `ValidationResult` with/without failures.
- For CachingBehaviour, use `TestCacheableQuery<Result<string>>` from TestKit as the request type.
- For handler tests, mock `IService<TestEntity>` and configure `.AddAsync()`, `.UpdateAsync()`, `.DeleteAsync()`, `.GetByKeyAsync()`, `.FindAsync()` return values.
- For validator tests, directly instantiate the validator and call `.Validate(command)`.
- For DI tests, use `new ServiceCollection()`, call `AddApplicationServices()`, inspect descriptors.
