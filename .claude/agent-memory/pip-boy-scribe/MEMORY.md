## Codebase Map

- Source projects: Abstractions, Application, OData, OData.FO, RELion, SampleFunction
- Test projects planned under `tests/` with 1:1 mapping + shared TestKit
- OData project has 5 key source files: ODataAuthenticationHandler, ODataMetadataProvider, ODataExceptionHandler, ODataService, ApplicationDependencyInjection
- Internal types in OData: `OperationContext` (record), `ODataNotFoundException` (exception) -- require InternalsVisibleTo
- ODataService uses static `ConcurrentDictionary<Type, CachedPropertyMetadata[]>` for payload metadata caching
- `CachedPropertyMetadata` is a private sealed record inside ODataService
- Simple.OData.Client fluent API: `IODataClient.For<T>()` returns `IBoundClient<T>` -- chain mocking needed
- `WebRequestException` from Simple.OData.Client wraps HTTP errors with `.Code` property (HttpStatusCode)
- RELion project has: RelionAuthenticationHandler, RelionService (3 public methods + private QueryAsync), query/handler, DI, interface
- RELion DTOs use Newtonsoft `[JsonProperty]`; `RelionCompany` uses System.Text.Json `[JsonPropertyName]` (dual serializer issue)
- RELion service uses `IHttpClientFactory.CreateClient("RelionApiClient")` -- mock factory returns HttpClient with FakeHttpMessageHandler
- RelionService.QueryAsync mutates the `filters` list by appending a DONE entry each call (potential bug in pagination loop)
- `RelionResponseEntity.EncodedResponseJson` maps to JSON property `"ResponseJson2"` (not obvious)
- OData.FO has 12 test groups: 4 entity types, dimension builder, delimiter extensions, dimension query/validator/handler, create/update handlers, command logging, DI
- `UpdateLedgerJournalHandler<TEntity>` is the batch update header handler (not UpdateLedgerJournalHeadersHandler) -- naming inconsistency
- `DimensionSegmentDelimiterExtensions.GetCharValue()` only handles Hyphen; all others throw
- `CreateLedgerJournalLinesCommand.GetLoggingContext()` key is "JournalNames" but values are batch numbers
- `UpdateLedgerJournalLinesCommand` uses `.Distinct()` on JournalBatchNumbers

## Test Infrastructure

- Stack: xUnit.v3, NSubstitute 5.3.x, FluentAssertions 8.x, Microsoft.NET.Test.Sdk 18.x
- TestKit provides: TestEntity, TestSingleKeyEntity, TestEntityWithODataAttributes, FakeHttpMessageHandler, FakeCacheService, custom ResultAssertions
- All test projects target net10.0 with LangVersion preview
- British spelling enforced: `Behaviour` not `Behavior`
- Naming: `MethodName_Scenario_ExpectedResult`
- AAA pattern mandatory with clear section separation
- DelegatingHandler testing: use `HttpMessageInvoker(sut)` with `sut.InnerHandler = fakeHandler`
- RelionService testing: mock IHttpClientFactory, queue responses on FakeHttpMessageHandler, build Base64-encoded response fixtures
- FluentValidation `TestValidate()` extension used for validator tests

## Sizing

- odata-tests: Estimated L (~90 min). 59 tests across 5 classes. ODataExceptionHandler has 19 tests. ODataService has 23 tests requiring fluent API mock chains.
- odata-fo-tests: Estimated M (~60 min). 56 tests across 15 classes. Most are small (entity, builder, command logging). Dimension query handler is the most complex.
- relion-tests: Estimated L (~90 min). 38-41 tests across 6 classes. RelionService alone has 17 tests with HTTP fixture construction and Base64 encoding.

## Spec Quality

- First enrichment (odata-tests): reference doc provided detailed test lists. Enrichment adds mock setup patterns, key implementation notes, edge cases, and TDD code snippets.
- Parameterised [Theory] tests recommended for HTTP status code mappings to reduce boilerplate.
- CreatePayload testing via captured mock arguments (Arg.Do pattern) since method is private static.
- Second enrichment (relion-tests): helper methods for Base64 fixture construction valuable. Dual JSON serializer issue flagged as edge case.
- Third enrichment (odata-fo): documenting class naming inconsistencies (e.g. UpdateLedgerJournalHandler) prevents Vault Dweller confusion. Recommended implementation order by increasing mock complexity speeds up feedback.
- Worktree has two mission file sets: `{name}-tests/mission.md` (newer) and `{name}/mission.md` (original stubs). Both are valid targets for enrichment depending on task assignment.
