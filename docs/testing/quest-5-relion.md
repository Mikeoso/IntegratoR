# Quest 5: RELion (RELion Integration Layer)

**Project**: `tests/IntegratoR.RELion.Tests/`
**Scope**: RELion authentication handler, HTTP service with pagination/Base64, CQRS queries, DTO serialization.
**Total**: 6 missions, ~41 tests

---

## Mission 5.1: RelionAuthenticationHandlerTests [M]

**Source**: `IntegratoR.RELion/Common/Authentication/RelionAuthenticationHandler.cs`
**Mocks**: `IOptions<RelionSettings>`, `IAuthenticator`, `FakeHttpMessageHandler`

| # | Test Method |
|---|---|
| 1 | `SendAsync_OAuthMode_SuccessfulToken_AddsBearerHeader` |
| 2 | `SendAsync_OAuthMode_FailedToken_Returns401Unauthorized` |
| 3 | `SendAsync_ApiKeyMode_AddsSubscriptionKeyHeader` |
| 4 | `SendAsync_OAuthMode_UsesRelionSpecificSettings` |

---

## Mission 5.2: RelionServiceTests [L]

**Source**: `IntegratoR.RELion/Common/Services/RelionService.cs`
**Mocks**: `IHttpClientFactory`, `ILogger<RelionService>`, `IOptions<RelionSettings>`

| # | Test Method |
|---|---|
| 1 | `GetCompanyByNameAsync_CompanyFound_ReturnsCompany` |
| 2 | `GetCompanyByNameAsync_CompanyNotFound_ReturnsNotFoundError` |
| 3 | `GetCompanyByNameAsync_CaseInsensitiveMatch_FindsCompany` |
| 4 | `GetCompanyByNameAsync_ApiError_ReturnsFailure` |
| 5 | `GetCompanyByNameAsync_Exception_ReturnsFailureWithException` |
| 6 | `GetLedgerAccountMappingsAsync_MappingFound_ReturnsMapping` |
| 7 | `GetLedgerAccountMappingsAsync_NoMapping_ReturnsEmptyMapping` |
| 8 | `GetLedgerAccountMappingsAsync_CompanyNotFound_ReturnsError` |
| 9 | `GetLedgerAccountMappingsAsync_QueryFails_ReturnsError` |
| 10 | `GetNewJournalLinesAsync_SinglePage_ReturnsAllLines` |
| 11 | `GetNewJournalLinesAsync_MultiplePages_PaginatesAndAggregates` |
| 12 | `GetNewJournalLinesAsync_EmptyResponse_ReturnsEmptyList` |
| 13 | `GetNewJournalLinesAsync_CompanyNotFound_ReturnsError` |
| 14 | `GetNewJournalLinesAsync_PageQueryFails_ReturnsError` |
| 15 | `QueryAsync_ValidResponse_DecodesBase64AndDeserializes` |
| 16 | `QueryAsync_NullEncodedResponseJson_ReturnsEmptyList` |
| 17 | `QueryAsync_DateFilterFormat_UsesIso8601` |

---

## Mission 5.3: GetRelionLedgerAccountMappingQueryTests [S]

**Source**: `IntegratoR.RELion/Features/Queries/Ledger/GetLedgerAccountMapping/GetRelionLedgerAccountMappingQuery.cs`
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `CacheKey_ContainsEntryNo` |
| 2 | `CacheDuration_Is30Minutes` |
| 3 | `GetLoggingContext_ContainsEntryNo` |

---

## Mission 5.4: GetRelionLedgerAccountMappingHandlerTests [S]

**Source**: `IntegratoR.RELion/Features/Queries/Ledger/GetLedgerAccountMapping/GetRelionLedgerAccountMappingHandler.cs`
**Mocks**: `ILogger`, `IRelionService`

| # | Test Method |
|---|---|
| 1 | `Handle_ServiceReturnsSuccess_ReturnsSuccessWithMapping` |
| 2 | `Handle_ServiceReturnsFailure_ReturnsFailure` |
| 3 | `Handle_DelegatesToRelionServiceWithCorrectEntryNo` |

---

## Mission 5.5: RelionDtoSerializationTests [M]

**Source**: `IntegratoR.RELion/Domain/DTOs/` and `IntegratoR.RELion/Domain/Models/`
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `RelionDataWrapper_Deserialize_ParsesDataArray` |
| 2 | `RelionCompanyDataWrapper_Deserialize_ParsesValueArray` |
| 3 | `RelionRequest_Serialize_UsesNewtonsoftPropertyNames` |
| 4 | `RelionRequestFilter_Serialize_NullValueHandling_ExcludesNulls` |
| 5 | `RelionResponseEntity_Deserialize_ParsesAllFields` |
| 6 | `RelionResponsePayload_Deserialize_ParsesEntitySet` |
| 7 | `RelionResponseEntity_EncodedResponseJson_CanBeBase64Decoded` |
| 8 | `RelionLedgerJournalLine_Deserialize_MapsNewtonJsonProperties` |

---

## Mission 5.6: RelionDependencyInjectionTests [S]

**Source**: `IntegratoR.RELion/Common/Extensions/ApplicationDependencyInjection.cs`
**Mocks**: None

| # | Test Method |
|---|---|
| 1 | `AddRelionClient_RegistersRelionService_AsScoped` |
| 2 | `AddRelionClient_RegistersAuthHandler_AsTransient` |
| 3 | `AddRelionClient_RegistersNamedHttpClient` |
