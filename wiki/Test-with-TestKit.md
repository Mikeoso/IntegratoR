# Test with TestKit

`IntegratoR.TestKit` is a shared test-support library that provides `Result<T>`-aware assertions, fakes for the cache and HTTP layers, and pre-built test entities. The TestKit is used by the framework's own tests and is published as a NuGet package for consumers to use in their own test suites.

> The TestKit is **not** auto-registered by `AddIntegratoR`. Add a `<PackageReference Include="IntegratoR.TestKit" />` to test projects only.

The framework's tests use xUnit v3, FluentAssertions, and NSubstitute. The TestKit assertions extend FluentAssertions — using a different assertion library is possible but the result-aware extensions only work with FluentAssertions.

## Assert on `Result` and `Result<T>`

The TestKit extends FluentAssertions with custom assertions that produce clear failure messages:

```csharp
using IntegratoR.TestKit.Assertions;

// Successful result
result.Should().BeSuccessful();

// Failed result
result.Should().BeFailed();

// Specific error code
result.Should().HaveErrorCode("OData.NotFound");

// Specific error type
result.Should().HaveErrorType(ErrorType.Validation);

// Successful result with a specific value (generic Result<T> only)
result.Should().HaveValue(expectedEntity);
```

`BeSuccessful()` on a failed result produces a message like *"Expected result to be successful, but it failed with: [OData.NotFound] Entity not found"* — the failure message embeds the error code and message so the test failure is self-describing.

The naming is intentional — `BeSuccessful` / `BeFailed` (not `BeSuccess` / `BeFailure`). Tests that use the older shape will not compile against the current TestKit.

## Fake `ICacheService`

`FakeCacheService` is an in-memory `ICacheService` implementation suitable for verifying the `CachingBehaviour` interaction in tests:

```csharp
using IntegratoR.TestKit.Fakes;

var cache = new FakeCacheService();

// Run code that exercises CachingBehaviour against this cache
await handler.Handle(query, cancellationToken);

// Inspect the cache directly
cache.Contains("MyQuery-key").Should().BeTrue();
cache.Count.Should().Be(1);

// Reset between tests
cache.Clear();
```

The fake stores entries forever — `CacheDuration` is ignored. Tests asserting on duration semantics need to mock `ICacheService` more deeply or use the production `DistributedCacheService` against a `MemoryDistributedCache`.

## Fake `HttpMessageHandler`

`FakeHttpMessageHandler` is a simple stub for HTTP-level tests:

```csharp
using IntegratoR.TestKit.Fakes;
using System.Net;

var handler = new FakeHttpMessageHandler(
    new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent("""{"value":[]}""")
    });

var httpClient = new HttpClient(handler);
```

The fake returns the configured response for every request. Tests that need different responses per call should use NSubstitute against `HttpMessageHandler` directly or stack multiple fakes.

## Test Entities

The TestKit ships three pre-built entities for scenarios where introducing a real D365 entity into a test would be noisy:

| Entity | Composite key | Purpose |
|---|---|---|
| `TestEntity` | `(Id, PartitionKey)` | Standard composite-key entity for generic command/query tests |
| `TestEntityWithODataAttributes` | `(Id, PartitionKey)` | Entity with `[ODataField]` annotations for serialisation-stripping tests |
| `TestEntityWithD365Attributes` | `(Id, PartitionKey)` | Entity with both `[ODataField]` and D365 CSDL-derived annotations (`AllowEdit`, `AllowEditOnCreate`) |
| `TestSingleKeyEntity` | `(Id)` | Entity with a single key — for non-D365 simple-key tests |

`TestEntityWithODataAttributes` is the right choice when testing that `ODataService<T>.AddAsync` correctly strips `IgnoreOnCreate = true` fields from the payload. `TestSingleKeyEntity` is for verifying that single-key composite-key handling does not assume an array length ≥ 2.

## Build Test Entities

`TestEntityBuilder` constructs `TestEntity` instances with sensible defaults:

```csharp
using IntegratoR.TestKit.Builders;

TestEntity entity = new TestEntityBuilder()
    .Default()
    .WithName("Custom Name")
    .WithPartitionKey("partition-1")
    .Build();
```

`Default()` populates every property with a reasonable test value (typically derived from the test method name + a random suffix). Override only the properties the test actually cares about — this keeps the test signal-to-noise high.

The builder pattern is implemented per-property as `With<PropertyName>(value)`. Test authors writing custom entities should follow the same convention so test fixtures stay consistent across the suite.

## Wire MediatR in Tests

For tests of a custom handler, register only the pieces the handler needs:

```csharp
ServiceCollection services = new();
services.AddSingleton<ICacheService>(new FakeCacheService());
services.AddSingleton<IService<TestEntity>>(Substitute.For<IService<TestEntity>>());
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MyHandler).Assembly));

ServiceProvider provider = services.BuildServiceProvider();
IMediator mediator = provider.GetRequiredService<IMediator>();
```

For integration tests of `AddIntegratoR` itself, build a `ConfigurationBuilder` with in-memory settings and call `AddIntegratoR(configuration)`:

```csharp
IConfiguration configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ODataSettings:Url"] = "https://example.com/data",
        ["ODataSettings:Authentication:Mode"] = "OAuth",
        ["ODataSettings:Authentication:OAuth:ClientId"] = "test"
    })
    .Build();

services.AddIntegratoR(configuration);
```

## Project Structure

The framework's test projects mirror the source project structure under `tests/`:

```
tests/
├── IntegratoR.Abstractions.Tests/
├── IntegratoR.Application.Tests/
├── IntegratoR.OData.Tests/
├── IntegratoR.OData.FO.Tests/
├── IntegratoR.RELion.Tests/
├── IntegratoR.Hosting.Tests/
├── IntegratoR.TestKit/         (the shared TestKit library)
└── IntegratoR.TestKit.Tests/   (tests for the TestKit itself)
```

Consumer test projects should reference the TestKit as a NuGet package — `ProjectReference` only works when building the framework alongside the consumer.

## Run Tests

```bash
dotnet test                                                     # all tests across the solution
dotnet test tests/IntegratoR.OData.Tests                        # one project
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"  # one test
```

The test suite as of v1.3.5 has 523 tests across 8 test projects, with three skipped (MSAL tests that require live credentials).

## What to Test, What to Skip

The repository follows a "value-leading" test discipline — tests that prove behaviour earn their keep, tests that mirror trivial structure are skipped:

- ✅ Pipeline behaviours (logging, validation, caching) — exercises real cross-cutting logic
- ✅ Custom handlers with non-trivial composition or error handling
- ✅ Serialisation contracts (`[ODataField]`, `[JsonPropertyName]`, `Result<T>` round-trip)
- ✅ LINQ-to-OData filter translation (one test per supported shape)
- ✅ Regression pins for fixed bugs (composite-key URL construction, BaseAddress normalisation, enum qualified-type form)
- ❌ POCO property getters/setters
- ❌ DI registration verifying every interface resolves
- ❌ Trivial pass-through delegation

## See Also

- [Handle Errors](Handle-Errors) — `Result<T>` shape that the assertions target
- [Add Validation](Add-Validation) — testing validator behaviour through the pipeline
- [Cache Query Results](Cache-Query-Results) — `FakeCacheService` usage patterns
- [Extend the Pipeline](Extend-the-Pipeline) — testing custom commands and behaviours
