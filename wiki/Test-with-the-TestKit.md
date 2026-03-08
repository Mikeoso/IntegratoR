# Test with the TestKit

Use the `IntegratoR.TestKit` library to write unit tests with pre-built fakes, test builders, and custom Result assertions.

> **Prerequisites:** [[Install-the-Framework]]

## Set Up a Test Project

Add a reference to `IntegratoR.TestKit` in your test project:

```xml
<ProjectReference Include="..\..\tests\IntegratoR.TestKit\IntegratoR.TestKit.csproj" />
```

The TestKit depends on xUnit v3, FluentAssertions 8.8.0, and NSubstitute 5.3.0.

## Assert on Results with BeSuccessful and BeFailed

The TestKit provides FluentAssertions extensions for `Result` and `Result<T>`:

```csharp
using FluentAssertions;
using FluentResults;
using IntegratoR.TestKit.Assertions;

[Fact]
public void Successful_result_should_pass_assertion()
{
    Result<string> result = Result.Ok("hello");

    result.Should().BeSuccessful();
}

[Fact]
public void Failed_result_should_pass_failure_assertion()
{
    Result<string> result = Result.Fail<string>("something went wrong");

    result.Should().BeFailed();
}
```

## Assert on Error Codes and Types

```csharp
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.TestKit.Assertions;

[Fact]
public void Should_have_expected_error_code()
{
    var error = new IntegrationError("Entity.NotFound", "Entity was not found", ErrorType.NotFound);
    Result result = Result.Fail(error);

    result.Should().BeFailed();
    result.Should().HaveErrorCode("Entity.NotFound");
    result.Should().HaveErrorType(ErrorType.NotFound);
}
```

## Assert on Result Values

```csharp
[Fact]
public void Should_have_expected_value()
{
    Result<int> result = Result.Ok(42);

    result.Should().BeSuccessful();
    result.Should().HaveValue(42);
}
```

## Use FakeCacheService for Cache Testing

`FakeCacheService` is an in-memory implementation of `ICacheService` with test-helper methods:

```csharp
using IntegratoR.TestKit.Fakes;

[Fact]
public async Task Should_cache_and_retrieve_value()
{
    var cache = new FakeCacheService();

    await cache.SetAsync("my-key", "cached-value", TimeSpan.FromMinutes(5));

    cache.Count.Should().Be(1);
    cache.Contains("my-key").Should().BeTrue();

    string? retrieved = await cache.GetAsync<string>("my-key");
    retrieved.Should().Be("cached-value");
}

[Fact]
public async Task Should_remove_cached_value()
{
    var cache = new FakeCacheService();
    await cache.SetAsync("my-key", "value");

    await cache.RemoveAsync("my-key");

    cache.Count.Should().Be(0);
    cache.Contains("my-key").Should().BeFalse();
}

[Fact]
public async Task Clear_removes_all_entries()
{
    var cache = new FakeCacheService();
    await cache.SetAsync("key1", "value1");
    await cache.SetAsync("key2", "value2");

    cache.Clear();

    cache.Count.Should().Be(0);
}
```

## Mock HTTP Responses with FakeHttpMessageHandler

`FakeHttpMessageHandler` lets you queue HTTP responses in FIFO order and inspect sent requests:

```csharp
using System.Net;
using IntegratoR.TestKit.Fakes;

[Fact]
public async Task Should_return_queued_responses_in_order()
{
    var handler = new FakeHttpMessageHandler();

    // Queue two responses
    handler.Queue(HttpStatusCode.OK, """{"id": 1, "name": "Test"}""");
    handler.Queue(HttpStatusCode.NotFound);

    HttpClient client = handler.CreateClient();

    // First request gets 200 OK
    HttpResponseMessage first = await client.GetAsync("https://api.example.com/items/1");
    first.StatusCode.Should().Be(HttpStatusCode.OK);

    string body = await first.Content.ReadAsStringAsync();
    body.Should().Contain("Test");

    // Second request gets 404 Not Found
    HttpResponseMessage second = await client.GetAsync("https://api.example.com/items/999");
    second.StatusCode.Should().Be(HttpStatusCode.NotFound);

    // Verify what was sent
    handler.SentRequests.Should().HaveCount(2);
    handler.SentRequests[0].RequestUri!.AbsolutePath.Should().Be("/items/1");
    handler.SentRequests[1].RequestUri!.AbsolutePath.Should().Be("/items/999");
}
```

If you dequeue more responses than you queued, the handler throws:

```csharp
[Fact]
public async Task Should_throw_when_no_queued_responses()
{
    var handler = new FakeHttpMessageHandler();
    HttpClient client = handler.CreateClient();

    Func<Task> act = () => client.GetAsync("https://api.example.com/items/1");

    await act.Should().ThrowAsync<InvalidOperationException>()
        .WithMessage("No more queued responses*");
}
```

## Build Test Entities with TestEntityBuilder

`TestEntityBuilder` provides a fluent API with sensible defaults for test data:

```csharp
using IntegratoR.TestKit.Builders;
using IntegratoR.TestKit.Doubles.Entities;

[Fact]
public void Should_build_entity_with_defaults()
{
    TestEntity entity = TestEntityBuilder.Default().Build();

    entity.Id.Should().Be("test-id");
    entity.PartitionKey.Should().Be("test-partition");
    entity.Name.Should().Be("Test Entity");
    entity.Description.Should().BeNull();
}

[Fact]
public void Should_override_specific_properties()
{
    TestEntity entity = TestEntityBuilder.Default()
        .WithId("custom-id")
        .WithName("Custom Entity")
        .WithDescription("A test entity for validation")
        .Build();

    entity.Id.Should().Be("custom-id");
    entity.Name.Should().Be("Custom Entity");
    entity.Description.Should().Be("A test entity for validation");
}
```

## Write a Complete Test

Here is a full test combining multiple TestKit utilities to test a handler:

```csharp
using System.Net;
using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.TestKit.Assertions;
using IntegratoR.TestKit.Fakes;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace MyApp.Tests;

public class MyHandlerTests
{
    private readonly IService<MyEntity> _service = Substitute.For<IService<MyEntity>>();
    private readonly ILogger<MyHandler> _logger = Substitute.For<ILogger<MyHandler>>();
    private readonly FakeCacheService _cache = new();

    [Fact]
    public async Task Handle_returns_success_when_service_succeeds()
    {
        // Arrange
        var entity = new MyEntity { Id = "1", Name = "Test" };
        _service.AddAsync(entity, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(entity));

        var handler = new MyHandler(_logger, _service);
        var command = new CreateMyEntityCommand(entity);

        // Act
        Result<MyEntity> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Should().HaveValue(entity);
    }

    [Fact]
    public async Task Handle_returns_failure_when_service_fails()
    {
        // Arrange
        var entity = new MyEntity { Id = "1", Name = "Test" };
        var error = new IntegrationError("Service.Failed", "OData error", ErrorType.Failure);
        _service.AddAsync(entity, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<MyEntity>(error));

        var handler = new MyHandler(_logger, _service);
        var command = new CreateMyEntityCommand(entity);

        // Act
        Result<MyEntity> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("Service.Failed");
        result.Should().HaveErrorType(ErrorType.Failure);
    }
}
```

## When Things Go Wrong

If you forget to import `IntegratoR.TestKit.Assertions`, the `Should()` method resolves to the default FluentAssertions `ObjectAssertions` and `BeSuccessful()` / `BeFailed()` are not available:

```csharp
// Missing: using IntegratoR.TestKit.Assertions;

result.Should().BeSuccessful(); // Compile error: 'ObjectAssertions' does not contain 'BeSuccessful'
```

Add the using directive:

```csharp
using IntegratoR.TestKit.Assertions;
```

## See Also

- [[Create-an-Entity]] — commands to test against
- [[Write-a-Specialized-Command]] — specialised commands with handlers to test
- [[Getting-Started]] — project setup
