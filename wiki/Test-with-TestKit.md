# Test with TestKit
> Last verified against v2.0.1

`IntegratoR.TestKit` gives test projects `Result<T>`-aware assertions, FIFO HTTP and cache fakes, and pre-built entities so a handler test reads like the behaviour it pins. Add a `<PackageReference Include="IntegratoR.TestKit" />` to test projects only — `AddIntegratoR` does not register it.

```csharp
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.TestKit.Assertions;
using NSubstitute;
using Xunit;

[Fact]
public async Task CreateHandler_ReturnsPersistedHeader()
{
    var header = new LedgerJournalHeader
    {
        DataAreaId = "USMF",
        JournalName = "GenJrnl",
        Description = "Month-end accrual"
    };

    IService<LedgerJournalHeader> service = Substitute.For<IService<LedgerJournalHeader>>();
    service.AddAsync(header, Arg.Any<CancellationToken>())
        .Returns(Result.Ok(header));

    var handler = new CreateCommandHandler<LedgerJournalHeader>(service);
    Result<LedgerJournalHeader> result =
        await handler.Handle(new CreateCommand<LedgerJournalHeader>(header), CancellationToken.None);

    result.Should().BeSuccessful();
    result.Should().HaveValue(header);
}
```

The five assertions live in `IntegratoR.TestKit.Assertions` and chain off `.Should()`: `BeSuccessful()`, `BeFailed()`, `HaveErrorCode(string)`, `HaveErrorType(ErrorType)`, and `HaveValue(T)` (generic `Result<T>` only). `HaveErrorCode` and `HaveErrorType` read the first `IntegrationError` on the result, so they fail with a clear message if the result carries a plain `Result.Fail("...")`.

## Handle the failure path

Assert the rejection the same way you assert success — chain `BeFailed()` into the code or type. This pins the `Validation` error a create raises when `JournalName` is missing:

```csharp
[Fact]
public async Task CreateHandler_SurfacesValidationError()
{
    var header = new LedgerJournalHeader { DataAreaId = "USMF", JournalName = "GenJrnl", Description = "x" };

    IService<LedgerJournalHeader> service = Substitute.For<IService<LedgerJournalHeader>>();
    service.AddAsync(header, Arg.Any<CancellationToken>())
        .Returns(Result.Fail(new IntegrationError(
            "Validation.Error", "JournalName is required", ErrorType.Validation)));

    var handler = new CreateCommandHandler<LedgerJournalHeader>(service);
    Result<LedgerJournalHeader> result =
        await handler.Handle(new CreateCommand<LedgerJournalHeader>(header), CancellationToken.None);

    result.Should().BeFailed().And.HaveErrorType(ErrorType.Validation);
    result.Should().HaveErrorCode("Validation.Error");
}
```

`BeSuccessful()` on a failed result reports the embedded error — *"Expected result to be successful, but it failed with: JournalName is required"* — so a test failure names the cause without extra logging.

## Stub HTTP with FakeHttpMessageHandler

`FakeHttpMessageHandler` returns queued responses in FIFO order and records what was sent. Construct it parameterless, queue one response per expected call, then call `CreateClient()`:

```csharp
using System.Net;
using IntegratoR.TestKit.Fakes;

var handler = new FakeHttpMessageHandler();
handler.Queue(HttpStatusCode.OK, """{"value":[{"dataAreaId":"USMF"}]}""");   // (status, body)
handler.Queue(new HttpResponseMessage(HttpStatusCode.NoContent));            // pre-built message

HttpClient client = handler.CreateClient();

HttpResponseMessage first = await client.GetAsync(new Uri("https://host/data/LedgerJournalHeaders"));
first.StatusCode.Should().Be(HttpStatusCode.OK);

// Inspect what the code under test sent — bodies captured eagerly, survive request disposal.
handler.SentRequests.Should().HaveCount(1);
handler.SentRequestBodies[0].Should().BeNull();   // GET has no body
```

`SentRequests` and `SentRequestBodies` are index-aligned; a request with no content yields a `null` body. Queue a response for every request the code makes.

> [!CAUTION]
> `FakeHttpMessageHandler` throws `InvalidOperationException` — "No more queued responses" — when the code makes more requests than you queued. Queue one response per expected call, including each item of a batch write (composite-key batch Update/Delete issue one request per item).

## Fake the cache

`FakeCacheService` is an in-memory `ICacheService` for `CachingBehaviour` tests. It stores entries forever — `CacheDuration` is ignored — and exposes `Count`, `Contains`, and `Clear` for inspection:

```csharp
var cache = new FakeCacheService();

await cache.SetAsync("LedgerJournalHeaders-USMF", header);   // no CancellationToken on ICacheService

cache.Contains("LedgerJournalHeaders-USMF").Should().BeTrue();
cache.Count.Should().Be(1);
cache.Clear();   // reset between tests
```

To assert duration or eviction semantics, run the production `DistributedCacheService` against a `MemoryDistributedCache` instead — `FakeCacheService` cannot express them.

## Use the pre-built test entities

Four entities in `IntegratoR.TestKit.Doubles.Entities` cover generic-handler and serialisation tests without pulling a full D365 entity into the fixture. Each inherits the non-generic `BaseEntity` and overrides `GetCompositeKey()`.

| Entity | `GetCompositeKey()` | Use for |
|---|---|---|
| `TestEntity` | `[Id, PartitionKey]` | Generic command/query handler tests |
| `TestEntityWithODataAttributes` | `[Id]` | `[ODataField(IgnoreOnCreate/IgnoreOnUpdate)]` payload-stripping tests |
| `TestEntityWithD365Attributes` | `[DataAreaId, JournalBatchNumber!]` | `AllowEdit`/`AllowEditOnCreate`/`IsRequired` runtime-enforcement tests |
| `TestSingleKeyEntity` | `[Id]` | Single-key handling (no array-length ≥ 2 assumption) |

`TestEntityBuilder` builds `TestEntity` from a static `Default()` factory with fixed values — `Id "test-id"`, `PartitionKey "test-partition"`, `Name "Test Entity"`, `Description null` — then chain `With…` overrides for only what the test cares about:

```csharp
using IntegratoR.TestKit.Builders;

TestEntity entity = TestEntityBuilder.Default()   // static factory, not new TestEntityBuilder()
    .WithName("Custom Name")
    .WithPartitionKey("partition-1")
    .Build();
```

> [!NOTE]
> `Default()` is a **static** factory returning a fresh builder with hardcoded values — call `TestEntityBuilder.Default()`, not `new TestEntityBuilder().Default()`. Values are constant across runs (no random suffix), so two builders with the same overrides produce equal entities.

## Wire MediatR in a handler test

Register only what the handler resolves, then send through `IMediator`:

```csharp
ServiceCollection services = new();
services.AddSingleton<ICacheService>(new FakeCacheService());
services.AddSingleton<IService<TestEntity>>(Substitute.For<IService<TestEntity>>());
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MyHandler).Assembly));

ServiceProvider provider = services.BuildServiceProvider();
IMediator mediator = provider.GetRequiredService<IMediator>();
```

For a full-stack test of registration itself, build an in-memory `IConfiguration` and call the one public entry point, `AddIntegratoR` — not the internal `AddODataClient`/`AddApplicationServices` composition steps:

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

## What to test, what to skip

Pin behaviour; skip tests that mirror structure a compiler already guarantees.

- Pipeline behaviours (logging, validation, caching) — real cross-cutting logic.
- Handlers with real composition or error handling.
- Serialisation contracts (`[ODataField]`, `[JsonPropertyName]`, `Result<T>` round-trip).
- LINQ-to-OData filter translation — one test per supported shape.
- Regression pins for fixed bugs (composite-key URL construction, BaseAddress normalisation, enum qualified-type form).

Skip POCO getters/setters, DI-registration checks that every interface resolves, and pass-through delegation.

Run the suite with `dotnet test`; scope to one project with `dotnet test tests/IntegratoR.OData.Tests` or one test with `dotnet test --filter "FullyQualifiedName~ClassName.MethodName"`.

## See Also

- [Handle Errors](Handle-Errors)
- [Add Validation](Add-Validation)
- [Cache Query Results](Cache-Query-Results)
- [Extend the Pipeline](Extend-the-Pipeline)
