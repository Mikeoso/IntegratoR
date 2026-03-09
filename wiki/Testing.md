# Testing

```csharp
using IntegratoR.TestKit.Assertions;
using IntegratoR.TestKit.Builders;
using IntegratoR.TestKit.Fakes;

TestEntity entity = TestEntityBuilder.Default().WithId("J-001").Build();
Result<TestEntity> result = Result.Ok(entity);

result.Should().BeSuccessful();
result.Should().HaveValue(entity);
```

Reference `IntegratoR.TestKit` from your test project. It depends on xUnit v3, FluentAssertions, and NSubstitute.

```xml
<ProjectReference Include="..\..\tests\IntegratoR.TestKit\IntegratoR.TestKit.csproj" />
```

## Result Assertions

FluentAssertions extensions for `Result` and `Result<T>` (`public static ResultAssertions Should(this Result result)` / `public static ResultAssertions<T> Should<T>(this Result<T> result)`):

```csharp
Result<int> ok = Result.Ok(42);
ok.Should().BeSuccessful();
ok.Should().HaveValue(42);

var error = new IntegrationError("Entity.NotFound", "Not found", ErrorType.NotFound);
Result failed = Result.Fail(error);
failed.Should().BeFailed();
failed.Should().HaveErrorCode("Entity.NotFound");   // first IntegrationError code
failed.Should().HaveErrorType(ErrorType.NotFound);   // first IntegrationError type
```

| Assertion | Applies to | Description |
|-----------|-----------|-------------|
| `BeSuccessful()` | Both | Asserts `IsSuccess == true` |
| `BeFailed()` | Both | Asserts `IsFailed == true` |
| `HaveErrorCode(string)` | Both | First [[Error-Handling]] has the expected code |
| `HaveErrorType(ErrorType)` | Both | First [[Error-Handling]] has the expected type |
| `HaveValue(T)` | `Result<T>` only | Asserts success and value equality |

## FakeCacheService

Dictionary-backed `ICacheService` (`public sealed class FakeCacheService : ICacheService`) that ignores expiration times.

```csharp
var cache = new FakeCacheService();

await cache.SetAsync("key-1", "hello");
cache.Count;                    // 1
cache.Contains("key-1");        // true

string? value = await cache.GetAsync<string>("key-1"); // "hello"

await cache.RemoveAsync("key-1");
cache.Count;                    // 0

cache.Clear();                  // removes everything
```

## FakeHttpMessageHandler

Test double for `HttpMessageHandler` (`public sealed class FakeHttpMessageHandler : HttpMessageHandler`) that dequeues responses in FIFO order and records sent requests.

```csharp
var handler = new FakeHttpMessageHandler();
handler.Queue(HttpStatusCode.OK, """{"JournalBatchNumber":"JBN-001"}""");
handler.Queue(HttpStatusCode.NotFound);

HttpClient client = handler.CreateClient();

HttpResponseMessage r1 = await client.GetAsync("https://d365.example.com/data/Journals");
// r1.StatusCode == HttpStatusCode.OK

HttpResponseMessage r2 = await client.GetAsync("https://d365.example.com/data/Journals('X')");
// r2.StatusCode == HttpStatusCode.NotFound

handler.SentRequests.Count;         // 2
handler.SentRequests[0].Method;     // HttpMethod.Get
handler.SentRequests[0].RequestUri; // https://d365.example.com/data/Journals

// No more queued responses -- next call throws InvalidOperationException
```

## TestEntityBuilder

Fluent builder (`public sealed class TestEntityBuilder`) for `TestEntity : BaseEntity<string>` with sensible defaults.

```csharp
TestEntity entity = TestEntityBuilder.Default().Build();
// entity.Id == "test-id", entity.PartitionKey == "test-partition"
// entity.Name == "Test Entity", entity.Description == null

TestEntity custom = TestEntityBuilder.Default()
    .WithId("JBN-001")
    .WithPartitionKey("USMF")
    .WithName("Monthly Accruals")
    .WithDescription("Q1 2026 accruals")
    .Build();

custom.GetCompositeKey(); // ["JBN-001", "USMF"]
```

## TestCacheableQuery

Configurable `ICacheableQuery<TResponse>` implementation for testing the [[Extending-the-Pipeline|CachingBehaviour]].

```csharp
var query = new TestCacheableQuery<Result<string>>(
    cacheKey: "test-key",
    cacheDuration: TimeSpan.FromMinutes(5),
    cacheKeyValues: ["param1", "param2"]);

query.CacheKey;            // "test-key"
query.CacheDuration;       // 00:05:00
query.GetCacheKeyValues(); // ["param1", "param2"]
```

## Full Example

```csharp
public class CreateCommandHandlerTests
{
    private readonly FakeCacheService _cache = new();
    private readonly IService<TestEntity> _service = Substitute.For<IService<TestEntity>>();
    private readonly ILogger<MyHandler> _logger = Substitute.For<ILogger<MyHandler>>();

    [Fact]
    public async Task Create_ValidEntity_ReturnsSuccess()
    {
        TestEntity entity = TestEntityBuilder.Default()
            .WithId("E-001")
            .WithPartitionKey("USMF")
            .Build();

        _service.AddAsync(entity, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(entity));

        var handler = new MyHandler(_logger, _service);
        Result<TestEntity> result = await handler.Handle(
            new CreateCommand<TestEntity>(entity), CancellationToken.None);

        result.Should().BeSuccessful();
        result.Should().HaveValue(entity);
    }

    [Fact]
    public async Task Create_ServiceFails_ReturnsFailure()
    {
        TestEntity entity = TestEntityBuilder.Default().Build();
        var error = new IntegrationError("OData.Conflict", "Duplicate key", ErrorType.Conflict);

        _service.AddAsync(entity, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TestEntity>(error));

        var handler = new MyHandler(_logger, _service);
        Result<TestEntity> result = await handler.Handle(
            new CreateCommand<TestEntity>(entity), CancellationToken.None);

        result.Should().BeFailed();
        result.Should().HaveErrorCode("OData.Conflict");
        result.Should().HaveErrorType(ErrorType.Conflict);
    }
}
```

## See Also

- [[Error-Handling]] — `IntegrationError`, `ErrorType`, and `GetError()`
- [[Caching]] — `ICacheableQuery` (use `TestCacheableQuery` to test)
- [[Extending-the-Pipeline]] — pipeline behaviours to test
- [[Commands]] — generic commands used in handler tests
