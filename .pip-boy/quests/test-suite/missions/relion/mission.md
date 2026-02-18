---
codename: "relion"
title: "RELion Tests"
quest: "test-suite"
status: completed
complexity: "L"
depends_on: ["testkit"]
created: "2026-02-18"
updated: "2026-02-18"
---

## Objective

Create `tests/IntegratoR.RELion.Tests/` with comprehensive tests for the RELion integration layer. This covers 6 test classes totalling ~41 tests across:

1. **RelionAuthenticationHandler** (4 tests) -- DelegatingHandler injecting OAuth Bearer tokens or API subscription keys, mirroring the OData auth handler pattern but with `RelionSettings`.
2. **RelionService** (17 tests) -- HTTP service with pagination, Base64 response decoding, company lookup, and error handling for `GetNewJournalLinesAsync`, `GetLedgerAccountMappingsAsync`, and `GetCompanyByNameAsync`.
3. **GetRelionLedgerAccountMappingQuery** (3 tests) -- Cacheable query record with cache key, duration, and logging context.
4. **GetRelionLedgerAccountMappingHandler** (3 tests) -- MediatR handler delegating to `IRelionService`.
5. **DTO serialization** (8 tests) -- Newtonsoft.Json round-trips for `RelionRequest`, `RelionRequestFilter`, `RelionResponsePayload`, `RelionResponseEntity`, `RelionDataWrapper<T>`, `RelionCompanyDataWrapper<T>`, `RelionLedgerJournalLine`.
6. **DI registration** (3 tests) -- Service registration, auth handler, and named HttpClient.

## Approach

### Step 1: Project setup

The `.csproj` shell exists from the `testkit` mission. Ensure it references `IntegratoR.RELion` (project ref) and `IntegratoR.TestKit` (project ref). Mirror source structure:

```
tests/IntegratoR.RELion.Tests/
  Common/
    Authentication/
      RelionAuthenticationHandlerTests.cs
    Services/
      RelionServiceTests.cs
    Extensions/
      ApplicationDependencyInjectionTests.cs
  Features/
    Queries/
      Ledger/
        GetLedgerAccountMapping/
          GetRelionLedgerAccountMappingQueryTests.cs
          GetRelionLedgerAccountMappingHandlerTests.cs
  Domain/
    DTOs/
      RelionDtoSerializationTests.cs
```

### Step 2: RelionAuthenticationHandlerTests (4 tests)

**Source**: `IntegratoR.RELion/Common/Authentication/RelionAuthenticationHandler.cs`

**Mocks/Fakes**: `IOptions<RelionSettings>` via `Options.Create(...)`, `IAuthenticator` (NSubstitute), `FakeHttpMessageHandler` (TestKit) as inner handler.

The handler follows the same pattern as ODataAuthenticationHandler but uses `RelionSettings` and `RelionAuthMode`.

| # | Test | What to Assert |
|---|---|---|
| 1 | `SendAsync_OAuthMode_SuccessfulToken_AddsBearerHeader` | Request reaches inner handler; `Authorization` is `Bearer test-token` |
| 2 | `SendAsync_OAuthMode_FailedToken_Returns401Unauthorized` | Response status is 401; ReasonPhrase contains failure message with "Relion OAuth token" |
| 3 | `SendAsync_ApiKeyMode_AddsSubscriptionKeyHeader` | Request header `SubscriptionHeaderKey` equals configured `SubscriptionKey` |
| 4 | `SendAsync_OAuthMode_UsesRelionSpecificSettings` | `IAuthenticator.GetAccessTokenAsync` receives `RelionSettings.ClientId`, `ClientSecret`, `TenantId`, `Resource` |

**Key difference from OData handler**: ApiKey mode does NOT add `DefaultHeaders` (no such property on `RelionSettings`). It only adds the single subscription key header.

### Step 3: RelionServiceTests (17 tests)

**Source**: `IntegratoR.RELion/Common/Services/RelionService.cs`

**Mocks**: `IHttpClientFactory` (NSubstitute), `ILogger<RelionService>`, `IOptions<RelionSettings>`

The `RelionService` uses `IHttpClientFactory.CreateClient("RelionApiClient")` to get its `HttpClient`. Mock this by creating an `HttpClient` backed by `FakeHttpMessageHandler` and returning it from the factory mock.

**Setup pattern**:
```csharp
var fakeHandler = new FakeHttpMessageHandler();
var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("https://relion.test") };
var httpClientFactory = Substitute.For<IHttpClientFactory>();
httpClientFactory.CreateClient("RelionApiClient").Returns(httpClient);
var settings = Options.Create(new RelionSettings { Url = "https://relion.test", Company = "TestCompany" });
var logger = Substitute.For<ILogger<RelionService>>();
var sut = new RelionService(httpClientFactory, logger, settings);
```

**GetCompanyByNameAsync tests (5)**:
| # | Test | What to Assert |
|---|---|---|
| 1 | `GetCompanyByNameAsync_CompanyFound_ReturnsCompany` | Queue GET response with `RelionCompanyDataWrapper` JSON containing matching company; result is success |
| 2 | `GetCompanyByNameAsync_CompanyNotFound_ReturnsNotFoundError` | JSON has companies but none matching; error code `Relion.CompanyNotFound` |
| 3 | `GetCompanyByNameAsync_CaseInsensitiveMatch_FindsCompany` | Company name in response differs in case; still matches |
| 4 | `GetCompanyByNameAsync_ApiError_ReturnsFailure` | Queue 500 response; error code `Relion.ApiError` |
| 5 | `GetCompanyByNameAsync_Exception_ReturnsFailureWithException` | Queue throw (e.g., via FakeHttpMessageHandler); error code `Relion.Exception` |

**GetLedgerAccountMappingsAsync tests (4)**:
| # | Test | What to Assert |
|---|---|---|
| 6 | `GetLedgerAccountMappingsAsync_MappingFound_ReturnsMapping` | Queue company GET + POST response with Base64-encoded mapping data |
| 7 | `GetLedgerAccountMappingsAsync_NoMapping_ReturnsEmptyMapping` | POST returns empty data; result has `LedgerAccountNo = ""`, `TaxAccountNo = ""` |
| 8 | `GetLedgerAccountMappingsAsync_CompanyNotFound_ReturnsError` | Company GET returns no match; error code contains `CompanyNotFound` |
| 9 | `GetLedgerAccountMappingsAsync_QueryFails_ReturnsError` | POST returns 500; error propagated |

**GetNewJournalLinesAsync tests (5)**:
| # | Test | What to Assert |
|---|---|---|
| 10 | `GetNewJournalLinesAsync_SinglePage_ReturnsAllLines` | Queue company + 1 POST (MoreRows=false); returns all lines |
| 11 | `GetNewJournalLinesAsync_MultiplePages_PaginatesAndAggregates` | Queue company + 2 POSTs (first MoreRows=true, second MoreRows=false); aggregates both pages |
| 12 | `GetNewJournalLinesAsync_EmptyResponse_ReturnsEmptyList` | Queue company + POST with no data; returns empty list |
| 13 | `GetNewJournalLinesAsync_CompanyNotFound_ReturnsError` | Company lookup fails; error propagated |
| 14 | `GetNewJournalLinesAsync_PageQueryFails_ReturnsError` | POST returns 500; error propagated |

**QueryAsync private method (tested via public methods) (3)**:
| # | Test | What to Assert |
|---|---|---|
| 15 | `QueryAsync_ValidResponse_DecodesBase64AndDeserializes` | Response `EncodedResponseJson` is Base64; verify correct deserialization |
| 16 | `QueryAsync_NullEncodedResponseJson_ReturnsEmptyList` | `EncodedResponseJson` is null or empty; returns empty list |
| 17 | `QueryAsync_DateFilterFormat_UsesIso8601` | Capture POST request body; verify filter value uses ISO 8601 format (`>yyyy-MM-ddTHH:mm:ss.fffffffK`) |

**Key implementation notes**:
- `GetNewJournalLinesAsync` calls `GetCompanyByNameAsync` first, then pages through `QueryAsync`. Both the company and page responses must be queued on `FakeHttpMessageHandler`.
- Pagination is tested by queuing multiple POST responses. First response has `MoreRows = true` in `RelionResponseEntity`, second has `MoreRows = false`.
- Base64 encoding: The response payload's `RelionResponseEntity.EncodedResponseJson` contains a Base64 string that decodes to a `RelionDataWrapper<T>` JSON. Tests must create this two-layer encoding.

### Step 4: GetRelionLedgerAccountMappingQueryTests (3 tests)

**Source**: `IntegratoR.RELion/Features/Queries/Ledger/GetLedgerAccountMapping/GetRelionLedgerAccountMappingQuery.cs`

No mocks -- pure record tests.

- `CacheKey_ContainsEntryNo` -- `new GetRelionLedgerAccountMappingQuery(42).CacheKey` equals `"42"`
- `CacheDuration_Is30Minutes` -- `CacheDuration` equals `TimeSpan.FromMinutes(30)`
- `GetLoggingContext_ContainsEntryNo` -- Dictionary has key `"EntryNo"` with value `42`

### Step 5: GetRelionLedgerAccountMappingHandlerTests (3 tests)

**Source**: `IntegratoR.RELion/Features/Queries/Ledger/GetLedgerAccountMapping/GetRelionLedgerAccountMappingHandler.cs`

**Mocks**: `ILogger<GetRelionLedgerAccountMappingHandler>`, `IRelionService`

The handler simply delegates to `_relionService.GetLedgerAccountMappingsAsync(request.EntryNo, ct)` and returns the result directly.

| # | Test | What to Assert |
|---|---|---|
| 1 | `Handle_ServiceReturnsSuccess_ReturnsSuccessWithMapping` | Service returns `Result.Ok(mapping)`; handler returns same |
| 2 | `Handle_ServiceReturnsFailure_ReturnsFailure` | Service returns `Result.Fail`; handler propagates |
| 3 | `Handle_DelegatesToRelionServiceWithCorrectEntryNo` | `_relionService.GetLedgerAccountMappingsAsync` received call with matching `EntryNo` |

### Step 6: RelionDtoSerializationTests (8 tests)

**Source**: `IntegratoR.RELion/Domain/DTOs/` and `IntegratoR.RELion/Domain/Models/`

All DTO/model classes use Newtonsoft.Json (`[JsonProperty]`). Tests verify serialization round-trips.

| # | Test | What to Assert |
|---|---|---|
| 1 | `RelionDataWrapper_Deserialize_ParsesDataArray` | `{"data": [...]}` deserializes to `RelionDataWrapper<T>.Data` list |
| 2 | `RelionCompanyDataWrapper_Deserialize_ParsesValueArray` | `{"value": [...]}` deserializes to `RelionCompanyDataWrapper<T>.Data` list |
| 3 | `RelionRequest_Serialize_UsesNewtonsoftPropertyNames` | Serialized JSON uses `"tableNo"`, `"operation"`, `"entitySet"`, `"top"`, `"skip"` |
| 4 | `RelionRequestFilter_Serialize_NullValueHandling_ExcludesNulls` | Properties with null values are excluded when `DefaultValueHandling.Ignore` is used |
| 5 | `RelionResponseEntity_Deserialize_ParsesAllFields` | JSON with `"moreRows"`, `"encodedResponseJson"`, `"subOperation"` deserializes correctly |
| 6 | `RelionResponsePayload_Deserialize_ParsesEntitySet` | JSON with `"entitySet": [...]` deserializes to `RelionResponsePayload.EntitySet` |
| 7 | `RelionResponseEntity_EncodedResponseJson_CanBeBase64Decoded` | Set `EncodedResponseJson` to a Base64 string; decode and verify content |
| 8 | `RelionLedgerJournalLine_Deserialize_MapsNewtonJsonProperties` | JSON with Newtonsoft property names deserializes to correct .NET properties |

**Key**: Use `JsonConvert.SerializeObject` and `JsonConvert.DeserializeObject` with `new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore }` matching the production serialisation settings.

### Step 7: RelionDependencyInjectionTests (3 tests)

**Source**: `IntegratoR.RELion/Common/Extensions/ApplicationDependencyInjection.cs`

**No mocks** -- use real `ServiceCollection`.

| # | Test | What to Assert |
|---|---|---|
| 1 | `AddRelionClient_RegistersRelionService_AsScoped` | `IRelionService` is registered with `ServiceLifetime.Scoped` |
| 2 | `AddRelionClient_RegistersAuthHandler_AsTransient` | `RelionAuthenticationHandler` is registered with `ServiceLifetime.Transient` |
| 3 | `AddRelionClient_RegistersNamedHttpClient` | `IHttpClientFactory` can create `"RelionApiClient"` |

## Existing Code to Leverage

| Component | Path |
|---|---|
| RelionAuthenticationHandler | `IntegratoR.RELion/Common/Authentication/RelionAuthenticationHandler.cs` |
| RelionService | `IntegratoR.RELion/Common/Services/RelionService.cs` |
| RelionSettings / RelionAuthMode | `IntegratoR.RELion/Domain/Settings/` |
| IRelionService | `IntegratoR.RELion/Interfaces/Services/IRelionService.cs` |
| All DTOs | `IntegratoR.RELion/Domain/DTOs/` (RelionRequest, RelionRequestFilter, RelionResponsePayload, RelionResponseEntity, RelionDataWrapper, RelionCompanyDataWrapper) |
| All Models | `IntegratoR.RELion/Domain/Models/` (RelionCompany, RelionLedgerAccountMapping, RelionLedgerJournalLine) |
| Query/Handler | `IntegratoR.RELion/Features/Queries/Ledger/GetLedgerAccountMapping/` |
| DI registration | `IntegratoR.RELion/Common/Extensions/ApplicationDependencyInjection.cs` |
| FakeHttpMessageHandler | `tests/IntegratoR.TestKit/Fakes/` |
| IAuthenticator | `IntegratoR.Abstractions/Interfaces/Authentication/IAuthenticator.cs` |
| Custom Result assertions | `tests/IntegratoR.TestKit/Assertions/` |

## Edge Cases

- **RelionAuthenticationHandler ApiKey mode**: Only adds `SubscriptionHeaderKey: SubscriptionKey` header. No `DefaultHeaders` like OData handler. Test must NOT verify DefaultHeaders.
- **RelionService pagination**: `PageSize` is 500 (const). When `MoreRows` is true, `recordsToSkip` increments by 500 each page. Verify multiple page aggregation.
- **RelionService Base64 encoding**: Response `EncodedResponseJson` is Base64-encoded JSON wrapped in `RelionDataWrapper<T>`. Test must create the two-layer encoding: serialize inner data to JSON, then Base64-encode it.
- **RelionService company lookup**: `GetCompanyByNameAsync` uses `StringComparison.OrdinalIgnoreCase` for matching. Test case-insensitive matching.
- **RelionService mutates filters list**: `QueryAsync` calls `filters.Add(new RelionRequestFilter { SubOperation = "DONE", ... })` which modifies the caller's list. This is a side effect -- the second page query will have one extra filter. Tests should verify pagination still works.
- **RelionService null EncodedResponseJson**: When `EncodedResponseJson` is null/empty, `QueryAsync` returns `(new List<T>(), false)`.
- **GetRelionLedgerAccountMappingQuery cache key**: Simply returns `$"{EntryNo}"` (just the number as a string). Not namespaced.
- **GetRelionLedgerAccountMappingHandler**: Pure passthrough -- no transformation of the result. Handler returns `_relionService.GetLedgerAccountMappingsAsync()` directly.
- **DTO NullValueHandling**: `RelionService.QueryAsync` uses `JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore }` when serializing the request. This means default-valued properties (0 for int, false for bool) are excluded. Test that `Skip = 0` is excluded from serialized JSON.

## Expected File Changes

| Action | File Path |
|--------|-----------|
| CREATE | `tests/IntegratoR.RELion.Tests/Common/Authentication/RelionAuthenticationHandlerTests.cs` |
| CREATE | `tests/IntegratoR.RELion.Tests/Common/Services/RelionServiceTests.cs` |
| CREATE | `tests/IntegratoR.RELion.Tests/Common/Extensions/ApplicationDependencyInjectionTests.cs` |
| CREATE | `tests/IntegratoR.RELion.Tests/Features/Queries/Ledger/GetLedgerAccountMapping/GetRelionLedgerAccountMappingQueryTests.cs` |
| CREATE | `tests/IntegratoR.RELion.Tests/Features/Queries/Ledger/GetLedgerAccountMapping/GetRelionLedgerAccountMappingHandlerTests.cs` |
| CREATE | `tests/IntegratoR.RELion.Tests/Domain/DTOs/RelionDtoSerializationTests.cs` |

## Done When

1. All 6 test classes exist mirroring source structure
2. All ~41 tests pass via `dotnet test`
3. Auth handler tests verify both OAuth and ApiKey paths with correct Relion-specific settings
4. RelionService tests cover: company lookup (5 scenarios), ledger account mapping (4 scenarios), journal lines with pagination (5 scenarios), and Base64/date format verification (3 scenarios)
5. Query record tests verify cache key, duration, and logging context
6. Handler tests verify delegation and error propagation
7. DTO tests verify Newtonsoft.Json serialization round-trips for all 7 DTO/model types
8. DI tests verify service lifetime registrations and named HttpClient
9. Tests follow AAA pattern, British spelling, `MethodName_Scenario_ExpectedResult` naming
10. `FakeHttpMessageHandler` from TestKit used for all HTTP mocking (not NSubstitute for HttpClient)
11. No production code modified

## TDD Guidance

**Test framework**: xUnit.v3, NSubstitute 5.3.x, FluentAssertions 8.x
**HTTP mocking**: Use `FakeHttpMessageHandler` from TestKit (not NSubstitute for HttpClient)

**Recommended implementation order**:
1. `GetRelionLedgerAccountMappingQueryTests` -- pure record, 3 tests
2. `RelionDtoSerializationTests` -- JSON round-trips, 8 tests
3. `GetRelionLedgerAccountMappingHandlerTests` -- simple mock, 3 tests
4. `RelionAuthenticationHandlerTests` -- DelegatingHandler pattern, 4 tests
5. `RelionDependencyInjectionTests` -- ServiceCollection, 3 tests
6. `RelionServiceTests` -- most complex (HTTP mocking, pagination, Base64), 17 tests

**Key mock patterns**:

```csharp
// RelionService test setup
var fakeHandler = new FakeHttpMessageHandler();
var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("https://relion.test") };
var factory = Substitute.For<IHttpClientFactory>();
factory.CreateClient("RelionApiClient").Returns(httpClient);
var settings = Options.Create(new RelionSettings { Url = "https://relion.test", Company = "TestCo" });
var logger = Substitute.For<ILogger<RelionService>>();
var sut = new RelionService(factory, logger, settings);
```

```csharp
// Company response setup
var companyJson = JsonConvert.SerializeObject(new { value = new[] { new { id = "123", name = "TestCo" } } });
fakeHandler.Queue(HttpStatusCode.OK, companyJson);
```

```csharp
// Base64-encoded page response setup
var innerData = new { data = new[] { new RelionLedgerJournalLine { ... } } };
var innerJson = JsonConvert.SerializeObject(innerData);
var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(innerJson));
var responseEntity = new RelionResponseEntity { EncodedResponseJson = base64, MoreRows = false };
var payload = new RelionResponsePayload { EntitySet = new List<RelionResponseEntity> { responseEntity } };
var payloadJson = JsonConvert.SerializeObject(payload);
fakeHandler.Queue(HttpStatusCode.OK, payloadJson);
```

```csharp
// Auth handler test (same pattern as OData auth tests)
var settings = Options.Create(new RelionSettings { AuthMode = RelionAuthMode.OAuth, ClientId = "id", ... });
var authenticator = Substitute.For<IAuthenticator>();
authenticator.GetAccessTokenAsync("id", "secret", "tenant", "resource").Returns(Result.Ok("token"));
var innerHandler = new FakeHttpMessageHandler();
innerHandler.Queue(HttpStatusCode.OK);
var handler = new RelionAuthenticationHandler(settings, authenticator) { InnerHandler = innerHandler };
var invoker = new HttpMessageInvoker(handler);
```

## Reference

See `docs/testing/quest-5-relion.md` for full test matrix.
