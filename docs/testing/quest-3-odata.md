# Quest 3: OData (Infrastructure -- Generic OData Client)

**Project**: `tests/IntegratoR.OData.Tests/`
**Scope**: Authentication handler, metadata provider, exception handler, OData service, DI registration. Requires mocking HTTP pipeline, `IAuthenticator`, `IODataClient`, Polly policies.
**Requires**: `InternalsVisibleTo("IntegratoR.OData.Tests")` in `IntegratoR.OData.csproj`
**Total**: 5 missions, ~59 tests

---

## Mission 3.1: ODataAuthenticationHandlerTests [M]

**Source**: `IntegratoR.OData/Common/Authentication/ODataAuthenticationHandler.cs`
**Mocks**: `IOptions<ODataSettings>`, `IAuthenticator`, `FakeHttpMessageHandler` (inner handler)

| # | Test Method |
|---|---|
| 1 | `SendAsync_OAuthMode_SuccessfulToken_AddsBearerHeader` |
| 2 | `SendAsync_OAuthMode_FailedToken_Returns401Unauthorized` |
| 3 | `SendAsync_OAuthMode_FailedToken_DoesNotCallInnerHandler` |
| 4 | `SendAsync_ApiKeyMode_AddsSubscriptionKeyHeader` |
| 5 | `SendAsync_ApiKeyMode_AddsDefaultHeaders` |
| 6 | `SendAsync_OAuthMode_PassesCorrectCredentials` |

---

## Mission 3.2: ODataMetadataProviderTests [M]

**Source**: `IntegratoR.OData/Common/Services/ODataMetadataProvider.cs`
**Mocks**: `ILogger<ODataMetadataProvider>` (uses real temp files)

| # | Test Method |
|---|---|
| 1 | `LoadMetadata_ValidXmlFile_ReturnsSuccessWithContent` |
| 2 | `LoadMetadata_FileNotFound_ReturnsError` |
| 3 | `LoadMetadata_InvalidXml_ReturnsValidationError` |
| 4 | `LoadMetadata_XmlWithDtd_RemovesDtdDeclarations` |
| 5 | `LoadMetadata_CalledTwice_ReturnsCachedResult` |
| 6 | `ClearCache_AfterLoad_ForcesReloadOnNextAccess` |

---

## Mission 3.3: ODataExceptionHandlerTests [L]

**Source**: `IntegratoR.OData/Common/Services/ODataExceptionHandler.cs`
**Mocks**: `ILogger`, optional `AsyncRetryPolicy`
**Requires**: `InternalsVisibleTo` for internal types

| # | Test Method |
|---|---|
| 1 | `ExecuteAsync_SuccessfulOperation_ReturnsOkResult` |
| 2 | `ExecuteAsync_WithRetryPolicy_RetriesOnTransientFailure` |
| 3 | `ExecuteAsync_WithoutRetryPolicy_ExecutesSingleAttempt` |
| 4 | `ExecuteCollectionAsync_Success_ReturnsOkWithEntities` |
| 5 | `ExecuteNonQueryAsync_Success_ReturnsOkResult` |
| 6 | `ExecuteScalarAsync_Success_ReturnsOkWithValue` |
| 7 | `HandleException_WebRequest401_ReturnsUnauthorizedError` |
| 8 | `HandleException_WebRequest400_ReturnsValidationError` |
| 9 | `HandleException_WebRequest404_ReturnsNotFoundError` |
| 10 | `HandleException_WebRequest409_ReturnsConflictError` |
| 11 | `HandleException_WebRequest412_ReturnsConcurrencyConflictError` |
| 12 | `HandleException_WebRequest429_ReturnsRateLimitExceededError` |
| 13 | `HandleException_WebRequest503_ReturnsServiceUnavailableError` |
| 14 | `HandleException_WebRequest5xx_ReturnsServerError` |
| 15 | `HandleException_TaskCanceledException_ReturnsTimeoutError` |
| 16 | `HandleException_OperationCanceledException_ReturnsCancelledError` |
| 17 | `HandleException_UnexpectedException_ReturnsUnexpectedError` |
| 18 | `ExecuteNonQueryAsync_NotFoundTreatAsSuccess_ReturnsOk` |
| 19 | `ExecuteNonQueryAsync_NotFoundNotTreatAsSuccess_ReturnsNotFoundError` |

---

## Mission 3.4: ODataServiceTests [L]

**Source**: `IntegratoR.OData/Common/Services/ODataService.cs`
**Mocks**: `IODataClient`, `ILogger<ODataService<TestEntity>>`

| # | Test Method |
|---|---|
| 1 | `AddAsync_ValidEntity_CallsInsertEntryAsync` |
| 2 | `AddAsync_CreatesPayloadWithCreateRules` |
| 3 | `GetByKeyAsync_EntityFound_ReturnsEntity` |
| 4 | `GetByKeyAsync_NullKeyValues_ReturnsValidationError` |
| 5 | `GetByKeyAsync_EmptyKeyValues_ReturnsValidationError` |
| 6 | `GetByKeyAsync_EntityNotFound_ReturnsNotFoundError` |
| 7 | `UpdateAsync_ValidEntity_CallsUpdateEntryAsync` |
| 8 | `UpdateAsync_NullEntity_ReturnsValidationError` |
| 9 | `DeleteAsync_ValidEntity_CallsDeleteEntryAsync` |
| 10 | `DeleteAsync_EntityNotFound_ReturnsSuccess` |
| 11 | `FindAsync_WithFilter_PassesFilterToClient` |
| 12 | `FindAsync_WithoutFilter_QueriesAll` |
| 13 | `QueryAsync_AllParameters_ChainsCorrectly` |
| 14 | `QueryAsync_PartialParameters_OnlyChainsProvided` |
| 15 | `CountAsync_WithFilter_ReturnsCount` |
| 16 | `AddBatchAsync_MultipleEntities_ExecutesBatch` |
| 17 | `UpdateBatchAsync_MultipleEntities_ExecutesBatch` |
| 18 | `DeleteBatchAsync_MultipleEntities_ExecutesBatch` |
| 19 | `CreatePayload_IgnoreOnCreate_ExcludesMarkedProperties` |
| 20 | `CreatePayload_IgnoreOnUpdate_ExcludesMarkedProperties` |
| 21 | `CreatePayload_JsonPropertyName_UsesJsonName` |
| 22 | `CreatePayload_NullValue_ExcludesProperty` |
| 23 | `CreatePayload_CachesPropertyMetadata` |

---

## Mission 3.5: ODataDependencyInjectionTests [S]

**Source**: `IntegratoR.OData/Common/Extensions/ApplicationDependencyInjection.cs`
**Mocks**: None -- real `ServiceCollection`

| # | Test Method |
|---|---|
| 1 | `AddODataClient_ConfigBased_BindsSettings` |
| 2 | `AddODataClient_ActionBased_BindsSettings` |
| 3 | `AddODataClient_RegistersAuthHandler_AsTransient` |
| 4 | `AddODataClient_RegistersMetadataProvider_AsSingleton` |
| 5 | `AddODataClient_RegistersNamedHttpClient` |
