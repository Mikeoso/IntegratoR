using System.Net;
using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.TestKit.Assertions;
using IntegratoR.TestKit.Builders;
using IntegratoR.TestKit.Fakes;
using Xunit;

namespace IntegratoR.TestKit.Tests;

/// <summary>
/// Smoke tests verifying the TestKit infrastructure components function correctly.
/// </summary>
public class TestKitSmokeTests
{
    /// <summary>
    /// Verifies that the default builder produces a valid TestEntity with expected defaults.
    /// </summary>
    [Fact]
    public void TestEntityBuilder_Default_Build_ReturnsEntityWithDefaults()
    {
        // Act
        var entity = TestEntityBuilder.Default().Build();

        // Assert
        entity.Id.Should().Be("test-id");
        entity.PartitionKey.Should().Be("test-partition");
        entity.Name.Should().Be("Test Entity");
        entity.Description.Should().BeNull();
    }

    /// <summary>
    /// Verifies that the builder returns different values when properties are overridden.
    /// </summary>
    [Fact]
    public void TestEntityBuilder_WithOverrides_ReturnsEntityWithOverriddenValues()
    {
        // Act
        var entity = TestEntityBuilder.Default()
            .WithId("custom-id")
            .WithPartitionKey("custom-partition")
            .WithName("Custom Name")
            .WithDescription("Custom Description")
            .Build();

        // Assert
        entity.Id.Should().Be("custom-id");
        entity.PartitionKey.Should().Be("custom-partition");
        entity.Name.Should().Be("Custom Name");
        entity.Description.Should().Be("Custom Description");
    }

    /// <summary>
    /// Verifies that the GetCompositeKey returns the composite key array.
    /// </summary>
    [Fact]
    public void TestEntity_GetCompositeKey_ReturnsIdAndPartitionKey()
    {
        // Arrange
        var entity = TestEntityBuilder.Default()
            .WithId("my-id")
            .WithPartitionKey("my-partition")
            .Build();

        // Act
        var key = entity.GetCompositeKey();

        // Assert
        key.Should().HaveCount(2);
        key[0].Should().Be("my-id");
        key[1].Should().Be("my-partition");
    }

    /// <summary>
    /// Verifies queued responses are returned FIFO.
    /// </summary>
    [Fact]
    public async Task FakeHttpMessageHandler_QueuedResponse_ReturnsFIFO()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        handler.Queue(HttpStatusCode.OK, "first");
        handler.Queue(HttpStatusCode.Created, "second");
        var client = handler.CreateClient();

        // Act
        var first = await client.GetAsync(new Uri("http://localhost/test1"), CancellationToken.None);
        var second = await client.GetAsync(new Uri("http://localhost/test2"), CancellationToken.None);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        handler.SentRequests.Should().HaveCount(2);
    }

    /// <summary>
    /// Verifies that calling SendAsync on an empty queue throws InvalidOperationException.
    /// </summary>
    [Fact]
    public async Task FakeHttpMessageHandler_EmptyQueue_ThrowsInvalidOperationException()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var client = handler.CreateClient();

        // Act
        var act = async () => await client.GetAsync(new Uri("http://localhost/test"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No more queued responses. Call Queue() before making HTTP requests.");
    }

    /// <summary>
    /// Verifies that FakeCacheService stores and retrieves values correctly.
    /// </summary>
    [Fact]
    public async Task FakeCacheService_SetAndGet_ReturnsCachedValue()
    {
        // Arrange
        var cache = new FakeCacheService();
        const string key = "test-key";
        const string value = "test-value";

        // Act
        await cache.SetAsync(key, value);
        var result = await cache.GetAsync<string>(key);

        // Assert
        result.Should().Be(value);
        cache.Contains(key).Should().BeTrue();
        cache.Count.Should().Be(1);
    }

    /// <summary>
    /// Verifies that ResultAssertions.BeSuccessful passes for a successful result.
    /// </summary>
    [Fact]
    public void ResultAssertions_BeSuccessful_OnSuccessResult_DoesNotThrow()
    {
        // Arrange
        var result = Result.Ok();

        // Act & Assert
        result.Should().BeSuccessful();
    }

    /// <summary>
    /// Verifies that ResultAssertions.HaveErrorCode matches the IntegrationError code.
    /// </summary>
    [Fact]
    public void ResultAssertions_HaveErrorCode_MatchesIntegrationError()
    {
        // Arrange
        var error = new IntegrationError("ERR001", "Something failed", ErrorType.Failure);
        var result = Result.Fail(error);

        // Act & Assert
        result.Should().BeFailed().And.HaveErrorCode("ERR001");
    }

    /// <summary>
    /// Verifies that ResultAssertions for generic Result T includes HaveValue assertion.
    /// </summary>
    [Fact]
    public void ResultAssertions_HaveValue_OnSuccessResultWithValue_DoesNotThrow()
    {
        // Arrange
        var result = Result.Ok(42);

        // Act & Assert
        result.Should().BeSuccessful().And.HaveValue(42);
    }

    /// <summary>
    /// Verifies that FakeCacheService RemoveAsync removes the entry.
    /// </summary>
    [Fact]
    public async Task FakeCacheService_RemoveAsync_RemovesEntry()
    {
        // Arrange
        var cache = new FakeCacheService();
        await cache.SetAsync("key", "value");

        // Act
        await cache.RemoveAsync("key");

        // Assert
        cache.Contains("key").Should().BeFalse();
        cache.Count.Should().Be(0);
    }

    /// <summary>
    /// Verifies that FakeCacheService Clear removes all entries.
    /// </summary>
    [Fact]
    public async Task FakeCacheService_Clear_RemovesAllEntries()
    {
        // Arrange
        var cache = new FakeCacheService();
        await cache.SetAsync("key1", "value1");
        await cache.SetAsync("key2", "value2");

        // Act
        cache.Clear();

        // Assert
        cache.Count.Should().Be(0);
    }
}
