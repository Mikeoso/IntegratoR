---
codename: "odata"
title: "OData Tests"
quest: "test-suite"
status: planned
complexity: "L"
depends_on: ["testkit"]
created: "2026-02-18"
updated: "2026-02-18"
---

## Objective

Create `tests/IntegratoR.OData.Tests/` with comprehensive tests for the generic OData infrastructure layer. This covers 5 test classes totalling ~59 tests across:

1. **ODataAuthenticationHandler** (6 tests) -- HTTP pipeline middleware attaching OAuth Bearer tokens or API subscription keys depending on `ODataAuthMode`.
2. **ODataMetadataProvider** (6 tests) -- Loads, sanitises (DTD removal), validates, and caches local XML metadata files.
3. **ODataExceptionHandler<TEntity>** (19 tests) -- Centralised exception handling mapping HTTP status codes and exception types to `IntegrationError` results.
4. **ODataService<TEntity>** (23 tests) -- Generic CRUD + batch + query service with payload construction respecting `[ODataField]`, `[JsonPropertyName]`, `[JsonIgnore]` attributes.
5. **ApplicationDependencyInjection** (5 tests) -- DI registration verification.

Requires `InternalsVisibleTo` for OData internal types (`OperationContext`, `ODataNotFoundException`).

## Approach

### Step 1: Project setup

The `.csproj` shell exists from `testkit` mission. Ensure it references `IntegratoR.OData` (project ref), `IntegratoR.TestKit` (project ref), and test packages. Verify `InternalsVisibleTo("IntegratoR.OData.Tests")` exists in `IntegratoR.OData.csproj`.

Mirror source folder structure:

```
tests/IntegratoR.OData.Tests/
  Common/
    Authentication/
      ODataAuthenticationHandlerTests.cs
    Services/
      ODataMetadataProviderTests.cs
      ODataExceptionHandlerTests.cs
      ODataServiceTests.cs
    Extensions/
      ApplicationDependencyInjectionTests.cs
```

### Step 2: ODataAuthenticationHandlerTests (6 tests)

**Source**: `IntegratoR.OData/Common/Authentication/ODataAuthenticationHandler.cs`

**Mocks/Fakes**: `IOptions<ODataSettings>` via `Options.Create(...)`, `IAuthenticator` (NSubstitute), `FakeHttpMessageHandler` (TestKit) as inner handler.

| # | Test | What to Assert |
|---|---|---|
| 1 | `SendAsync_OAuthMode_SuccessfulToken_AddsBearerHeader` | Request reaches inner handler; `Authorization` is `Bearer test-token` |
| 2 | `SendAsync_OAuthMode_FailedToken_Returns401Unauthorized` | Response status is 401; ReasonPhrase contains failure message |
| 3 | `SendAsync_OAuthMode_FailedToken_DoesNotCallInnerHandler` | `FakeHttpMessageHandler.SentRequests.Count` is 0 |
| 4 | `SendAsync_ApiKeyMode_AddsSubscriptionKeyHeader` | Request header contains subscription key value |
| 5 | `SendAsync_ApiKeyMode_AddsDefaultHeaders` | All entries from `ODataSettings.DefaultHeaders` present on request |
| 6 | `SendAsync_OAuthMode_PassesCorrectCredentials` | `IAuthenticator.GetAccessTokenAsync` received exact credential values |

**Setup**: Create handler, set `InnerHandler = fakeHandler`, wrap in `HttpMessageInvoker`, call `SendAsync`.

### Step 3: ODataMetadataProviderTests (6 tests)

**Source**: `IntegratoR.OData/Common/Services/ODataMetadataProvider.cs`

Uses real file I/O with `Path.GetTempFileName()`. Implement `IDisposable` to clean up temp files. Mock only `ILogger<ODataMetadataProvider>`.

| # | Test | What to Assert |
|---|---|---|
| 1 | `LoadMetadata_ValidXmlFile_ReturnsSuccessWithContent` | `result.IsSuccess`; content matches written XML |
| 2 | `LoadMetadata_FileNotFound_ReturnsError` | Error code `ODataMetadata.FileNotFound`; type `ErrorType.NotFound` |
| 3 | `LoadMetadata_InvalidXml_ReturnsValidationError` | Error code `ODataMetadata.ValidationFailed`; type `ErrorType.Validation` |
| 4 | `LoadMetadata_XmlWithDtd_RemovesDtdDeclarations` | Returned content has no `<!DOCTYPE` |
| 5 | `LoadMetadata_CalledTwice_ReturnsCachedResult` | Delete temp file before 2nd call; still succeeds from cache |
| 6 | `ClearCache_AfterLoad_ForcesReloadOnNextAccess` | Load, clear, delete file, load again; 2nd load fails |

### Step 4: ODataExceptionHandlerTests (19 tests)

**Source**: `IntegratoR.OData/Common/Services/ODataExceptionHandler.cs`

Uses `TestEntity` from TestKit. **Internal types** require InternalsVisibleTo: `OperationContext`, `ODataNotFoundException`.

| # | Test | What to Assert |
|---|---|---|
| 1 | `ExecuteAsync_SuccessfulOperation_ReturnsOkResult` | Result success with entity |
| 2 | `ExecuteAsync_WithRetryPolicy_RetriesOnTransientFailure` | Fails once, succeeds on retry |
| 3 | `ExecuteAsync_WithoutRetryPolicy_ExecutesSingleAttempt` | No retry; result failed |
| 4 | `ExecuteCollectionAsync_Success_ReturnsOkWithEntities` | IEnumerable wrapped in success |
| 5 | `ExecuteNonQueryAsync_Success_ReturnsOkResult` | Non-generic `Result.Ok()` |
| 6 | `ExecuteScalarAsync_Success_ReturnsOkWithValue` | Scalar value in success result |
| 7-14 | `HandleException_WebRequestException_MapsToCorrectError` | `[Theory]` with `[InlineData]` for 401/400/404/409/412/429/503/500 |
| 15 | `HandleException_TaskCanceledException_ReturnsTimeoutError` | Non-cancelled token => timeout |
| 16 | `HandleException_OperationCanceledException_ReturnsCancelledError` | Cancelled token => cancelled |
| 17 | `HandleException_UnexpectedException_ReturnsUnexpectedError` | `InvalidOperationException` => unexpected |
| 18 | `ExecuteNonQueryAsync_NotFoundTreatAsSuccess_ReturnsOk` | `ODataNotFoundException` + `treatNotFoundAsSuccess: true` |
| 19 | `ExecuteNonQueryAsync_NotFoundNotTreatAsSuccess_ReturnsNotFoundError` | Same exception, default => failed |

Use `[Theory]` with `[InlineData]` for HTTP status code tests (7-14) to consolidate.

### Step 5: ODataServiceTests (23 tests)

**Source**: `IntegratoR.OData/Common/Services/ODataService.cs`

**Key challenge**: Mock the `Simple.OData.Client` fluent API chain. `CreatePayload` is `private static` -- test indirectly via captured `Set()` arguments using `Arg.Do<object>()`.

CRUD (10), Query/Find/Count (5), Batch (3), CreatePayload (5).

### Step 6: ODataDependencyInjectionTests (5 tests)

**No mocks** -- use real `ServiceCollection` and `ConfigurationBuilder`.

Config-based/action-based settings binding, auth handler registration (Transient), metadata provider (Singleton), named HttpClient.

## Existing Code to Leverage

| Component | Path |
|---|---|
| ODataAuthenticationHandler | `IntegratoR.OData/Common/Authentication/ODataAuthenticationHandler.cs` |
| ODataMetadataProvider | `IntegratoR.OData/Common/Services/ODataMetadataProvider.cs` |
| ODataExceptionHandler | `IntegratoR.OData/Common/Services/ODataExceptionHandler.cs` |
| ODataService | `IntegratoR.OData/Common/Services/ODataService.cs` |
| ApplicationDependencyInjection | `IntegratoR.OData/Common/Extensions/ApplicationDependencyInjection.cs` |
| ODataSettings / ODataAuthMode | `IntegratoR.OData/Domain/Settings/` |
| IAuthenticator | `IntegratoR.Abstractions/Interfaces/Authentication/IAuthenticator.cs` |
| TestEntity, TestEntityWithODataAttributes | `tests/IntegratoR.TestKit/Doubles/Entities/` |
| FakeHttpMessageHandler | `tests/IntegratoR.TestKit/Fakes/` |
| Custom Result assertions | `tests/IntegratoR.TestKit/Assertions/` |

## Edge Cases

- **`WebRequestException` construction**: Check available constructors from Simple.OData.Client; `new WebRequestException(message, code)` is the expected signature.
- **`ODataNotFoundException` and `OperationContext` are internal**: Requires `InternalsVisibleTo` (testkit mission).
- **`TaskCanceledException` vs `OperationCanceledException`**: Timeout = `TaskCanceledException` when `CancellationToken` is NOT cancelled. Cancellation = `OperationCanceledException` with cancelled token.
- **`ODataService.PropertyMetadataCache` is static**: Between tests, cache retains entries. Tests relying on cache state should use unique entity types.
- **`IBoundClient<T>` fluent chain**: Each method must return the same mock for chaining. Set `.Returns(boundClient)` on all chain methods.
- **Batch tests with `ODataBatch`**: Concrete class, hard to intercept. Focus on success/failure verification rather than internal batch construction.
- **`ODataMetadataProvider` file paths**: Use absolute temp paths to avoid resolution ambiguity.
- **`CreatePayload` null/default exclusion**: Null reference types AND default value types (0, false) are excluded from the payload dictionary.

## Expected File Changes

| Action | File Path |
|--------|-----------|
| CREATE | `tests/IntegratoR.OData.Tests/Common/Authentication/ODataAuthenticationHandlerTests.cs` |
| CREATE | `tests/IntegratoR.OData.Tests/Common/Services/ODataMetadataProviderTests.cs` |
| CREATE | `tests/IntegratoR.OData.Tests/Common/Services/ODataExceptionHandlerTests.cs` |
| CREATE | `tests/IntegratoR.OData.Tests/Common/Services/ODataServiceTests.cs` |
| CREATE | `tests/IntegratoR.OData.Tests/Common/Extensions/ApplicationDependencyInjectionTests.cs` |

## Done When

1. All 5 test classes exist mirroring source structure under `Common/`
2. All 59 tests pass via `dotnet test`
3. ODataAuthenticationHandlerTests: 6 tests cover OAuth and ApiKey paths, header verification, short-circuit on token failure
4. ODataMetadataProviderTests: 6 tests use real temp files; caching/clearing verified
5. ODataExceptionHandlerTests: 19 tests cover all HTTP status code mappings, timeout vs cancellation, `treatNotFoundAsSuccess`
6. ODataServiceTests: 23 tests verify CRUD, query chaining, batch operations, CreatePayload attribute filtering
7. DI tests: 5 tests verify settings binding and service registration lifetimes
8. Tests follow AAA pattern, British spelling, `MethodName_Scenario_ExpectedResult` naming
9. No production code modified except InternalsVisibleTo (if not already added)

## TDD Guidance

**Test framework**: xUnit.v3 with `[Fact]` and `[Theory]`/`[InlineData]`
**Assertions**: FluentAssertions 8.x + custom `ResultAssertions` from TestKit
**Mocking**: NSubstitute 5.3.x

**Recommended implementation order** (by increasing mock complexity):
1. `ODataDependencyInjectionTests` -- simplest, no mocks
2. `ODataMetadataProviderTests` -- real files, minimal mocking
3. `ODataAuthenticationHandlerTests` -- straightforward DelegatingHandler
4. `ODataExceptionHandlerTests` -- complex exception construction, many cases
5. `ODataServiceTests` -- most complex mock chains

**Key mock patterns**:
```csharp
// Fluent chain mock
var boundClient = Substitute.For<IBoundClient<TestEntity>>();
_client.For<TestEntity>(null).Returns(boundClient);
boundClient.Set(Arg.Any<object>()).Returns(boundClient);
boundClient.Key(Arg.Any<object[]>()).Returns(boundClient);

// Payload capture
Dictionary<string, object>? captured = null;
boundClient.Set(Arg.Do<object>(p => captured = p as Dictionary<string, object>)).Returns(boundClient);

// Parameterised exception test
[Theory]
[InlineData(HttpStatusCode.Unauthorized, "Unauthorized", ErrorType.Failure)]
[InlineData(HttpStatusCode.BadRequest, "ValidationFailed", ErrorType.Validation)]
public async Task HandleException_WebRequestException_MapsToCorrectError(
    HttpStatusCode code, string suffix, ErrorType type) { ... }
```

## Reference

See `docs/testing/quest-3-odata.md` for full test matrix.
