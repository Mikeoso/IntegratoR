# TestKit

Shared test infrastructure providing fakes, builders, and custom FluentAssertions extensions for `Result` types. Reference `IntegratoR.TestKit` from your test projects.

## Use the TestKit

```csharp
using IntegratoR.TestKit.Assertions;
using IntegratoR.TestKit.Builders;
using IntegratoR.TestKit.Fakes;

var entity = TestEntityBuilder.Default().WithId("J-001").Build();
Result<TestEntity> result = Result.Ok(entity);

result.Should().BeSuccessful();
result.Should().HaveValue(entity);
```

## FakeCacheService

Dictionary-backed `ICacheService` for unit tests. Ignores expiration times.

```csharp
public sealed class FakeCacheService : ICacheService
```

### Members

| Member | Type | Description |
|--------|------|-------------|
| `Count` | `int` | Number of entries currently in the cache |
| `Contains(string cacheKey)` | `bool` | Whether the cache contains the given key |
| `Clear()` | `void` | Removes all entries |
| `GetAsync<T>(string cacheKey)` | `Task<T?>` | Retrieves a cached value |
| `SetAsync<T>(string cacheKey, T value, TimeSpan?)` | `Task` | Stores a value |
| `RemoveAsync(string cacheKey)` | `Task` | Removes an entry |

### Example

```csharp
var cache = new FakeCacheService();

await cache.SetAsync("key-1", "hello");
cache.Count;                    // 1
cache.Contains("key-1");        // true

string? value = await cache.GetAsync<string>("key-1");
// value == "hello"

await cache.RemoveAsync("key-1");
cache.Count;                    // 0

cache.Clear();                  // Removes everything
```

## FakeHttpMessageHandler

Test double for `HttpMessageHandler` that dequeues pre-configured responses in FIFO order and records all sent requests.

```csharp
public sealed class FakeHttpMessageHandler : HttpMessageHandler
```

### Members

| Member | Type | Description |
|--------|------|-------------|
| `Queue(HttpResponseMessage)` | `void` | Enqueues a pre-built response |
| `Queue(HttpStatusCode, string?)` | `void` | Enqueues a response with status code and optional body |
| `SentRequests` | `IReadOnlyList<HttpRequestMessage>` | All requests sent through this handler, in order |
| `CreateClient()` | `HttpClient` | Creates an `HttpClient` wired to this handler |

### Example

```csharp
var handler = new FakeHttpMessageHandler();

// Queue responses in expected call order
handler.Queue(HttpStatusCode.OK, """{"JournalBatchNumber":"JBN-001"}""");
handler.Queue(HttpStatusCode.NotFound);

HttpClient client = handler.CreateClient();

// First request gets 200 OK
HttpResponseMessage response1 = await client.GetAsync("https://d365.example.com/data/Journals");
// response1.StatusCode == HttpStatusCode.OK

// Second request gets 404
HttpResponseMessage response2 = await client.GetAsync("https://d365.example.com/data/Journals('X')");
// response2.StatusCode == HttpStatusCode.NotFound

// Inspect sent requests
handler.SentRequests.Count;         // 2
handler.SentRequests[0].Method;     // HttpMethod.Get
handler.SentRequests[0].RequestUri; // https://d365.example.com/data/Journals

// No more queued responses -- next call throws InvalidOperationException
```

## TestEntityBuilder

Fluent builder for `TestEntity` with sensible defaults.

```csharp
public sealed class TestEntityBuilder
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Default()` | `TestEntityBuilder` | Creates a builder with default values |
| `WithId(string)` | `TestEntityBuilder` | Sets the Id (default: `"test-id"`) |
| `WithPartitionKey(string)` | `TestEntityBuilder` | Sets the PartitionKey (default: `"test-partition"`) |
| `WithName(string)` | `TestEntityBuilder` | Sets the Name (default: `"Test Entity"`) |
| `WithDescription(string?)` | `TestEntityBuilder` | Sets the Description (default: `null`) |
| `Build()` | `TestEntity` | Creates the `TestEntity` instance |

### TestEntity

```csharp
public class TestEntity : BaseEntity<string>
{
    public required string Id { get; set; }
    public required string PartitionKey { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    public override object[] GetCompositeKey() => [Id, PartitionKey];
}
```

### Example

```csharp
// Default entity
TestEntity entity = TestEntityBuilder.Default().Build();
// entity.Id == "test-id"
// entity.PartitionKey == "test-partition"
// entity.Name == "Test Entity"
// entity.Description == null

// Customised entity
TestEntity custom = TestEntityBuilder.Default()
    .WithId("JBN-001")
    .WithPartitionKey("USMF")
    .WithName("Monthly Accruals")
    .WithDescription("Q1 2026 accruals")
    .Build();

custom.GetCompositeKey();  // ["USMF", "JBN-001"]
```

## Result Assertions

Custom FluentAssertions extensions for `Result` and `Result<T>`.

### ResultAssertionExtensions

```csharp
public static ResultAssertions Should(this Result result)
public static ResultAssertions<T> Should<T>(this Result<T> result)
```

### Available Assertions

| Assertion | Applies to | Description |
|-----------|-----------|-------------|
| `BeSuccessful()` | Both | Asserts `IsSuccess == true` |
| `BeFailed()` | Both | Asserts `IsFailed == true` |
| `HaveErrorCode(string)` | Both | Asserts the first `IntegrationError` has the expected code |
| `HaveErrorType(ErrorType)` | Both | Asserts the first `IntegrationError` has the expected type |
| `HaveValue(T)` | `Result<T>` only | Asserts success and value equality |

### Examples

```csharp
// Success assertions
Result<TestEntity> successResult = Result.Ok(entity);
successResult.Should().BeSuccessful();
successResult.Should().HaveValue(entity);

// Failure assertions
var error = new IntegrationError("OData.NotFound", "Not found", ErrorType.NotFound);
Result<TestEntity> failResult = Result.Fail<TestEntity>(error);

failResult.Should().BeFailed();
failResult.Should().HaveErrorCode("OData.NotFound");
failResult.Should().HaveErrorType(ErrorType.NotFound);

// Non-generic Result
Result okResult = Result.Ok();
okResult.Should().BeSuccessful();

Result failedResult = Result.Fail(error);
failedResult.Should().BeFailed();
failedResult.Should().HaveErrorCode("OData.NotFound");
```

## TestCacheableQuery\<TResponse\>

Configurable `ICacheableQuery` implementation for testing the `CachingBehaviour`.

```csharp
public sealed class TestCacheableQuery<TResponse> : ICacheableQuery<TResponse>
```

| Constructor Parameter | Type | Required | Default | Description |
|----------------------|------|----------|---------|-------------|
| `cacheKey` | `string` | Yes | -- | The static cache key |
| `cacheDuration` | `TimeSpan?` | No | `null` | Cache duration; `null` bypasses caching |
| `cacheKeyValues` | `object[]?` | No | `[]` | Values for `GetCacheKeyValues()` |

### Example

```csharp
var query = new TestCacheableQuery<Result<string>>(
    cacheKey: "test-key",
    cacheDuration: TimeSpan.FromMinutes(5),
    cacheKeyValues: ["param1", "param2"]);

query.CacheKey;          // "test-key"
query.CacheDuration;     // 00:05:00
query.GetCacheKeyValues(); // ["param1", "param2"]
```

## See a Full Test Example

```csharp
public class CreateCommandHandlerTests
{
    private readonly FakeCacheService _cache = new();
    private readonly IService<TestEntity> _service;
    private readonly IMediator _mediator;

    [Fact]
    public async Task Create_ValidEntity_ReturnsSuccess()
    {
        // Arrange
        TestEntity entity = TestEntityBuilder.Default()
            .WithId("E-001")
            .WithPartitionKey("USMF")
            .Build();

        _service.AddAsync(entity, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(entity));

        // Act
        Result<TestEntity> result = await _mediator.Send(
            new CreateCommand<TestEntity>(entity),
            cancellationToken);

        // Assert
        result.Should().BeSuccessful();
        result.Should().HaveValue(entity);
    }

    [Fact]
    public async Task Create_ServiceFails_ReturnsFailure()
    {
        // Arrange
        TestEntity entity = TestEntityBuilder.Default().Build();
        var error = new IntegrationError(
            "OData.Conflict", "Duplicate key", ErrorType.Conflict);

        _service.AddAsync(entity, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TestEntity>(error));

        // Act
        Result<TestEntity> result = await _mediator.Send(
            new CreateCommand<TestEntity>(entity),
            cancellationToken);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("OData.Conflict");
        result.Should().HaveErrorType(ErrorType.Conflict);
    }
}
```

## See Also

- [[API-IntegrationError]] — error type asserted with TestKit helpers
- [[API-Pipeline-Behaviours]] — behaviours tested with FakeCacheService
- [[API-ICacheableQuery]] — caching contract tested with FakeCacheService
- [[API-Generic-Commands]] — commands to use in integration tests
