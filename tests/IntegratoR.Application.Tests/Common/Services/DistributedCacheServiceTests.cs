using FluentAssertions;
using IntegratoR.Application.Common.Services;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using System.Text;
using System.Text.Json;
using Xunit;

namespace IntegratoR.Application.Tests.Common.Services;

/// <summary>
/// Tests for <see cref="DistributedCacheService"/> using a mocked <see cref="IDistributedCache"/>.
/// </summary>
public class DistributedCacheServiceTests
{
    private readonly IDistributedCache _distributedCache = Substitute.For<IDistributedCache>();

    private DistributedCacheService CreateSut() => new(_distributedCache);

    private static byte[] Serialize<T>(T value)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return JsonSerializer.SerializeToUtf8Bytes(value, options);
    }

    [Fact]
    public async Task GetAsync_ExistingKey_DeserializesAndReturnsValue()
    {
        // Arrange
        var sut = CreateSut();
        var bytes = Serialize("hello-world");
        _distributedCache.GetAsync("key", Arg.Any<CancellationToken>()).Returns(bytes);

        // Act
        var result = await sut.GetAsync<string>("key");

        // Assert
        result.Should().Be("hello-world");
    }

    [Fact]
    public async Task GetAsync_NonExistingKey_ReturnsDefault()
    {
        // Arrange
        var sut = CreateSut();
        _distributedCache.GetAsync("missing", Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        // Act
        var result = await sut.GetAsync<string>("missing");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_NullKey_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = async () => await sut.GetAsync<string>(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetAsync_EmptyBytes_ReturnsDefault()
    {
        // Arrange
        var sut = CreateSut();
        _distributedCache.GetAsync("key", Arg.Any<CancellationToken>()).Returns(Array.Empty<byte>());

        // Act
        var result = await sut.GetAsync<string>("key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ValidKeyAndValue_SerializesToUtf8BytesWithCamelCase()
    {
        // Arrange
        var sut = CreateSut();
        byte[]? capturedBytes = null;
        await _distributedCache.SetAsync(
            Arg.Any<string>(),
            Arg.Do<byte[]>(b => capturedBytes = b),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());

        var value = new { MyProperty = "test-value" };

        // Act
        await sut.SetAsync("key", value);

        // Assert -- verify camelCase serialization
        capturedBytes.Should().NotBeNull();
        var json = Encoding.UTF8.GetString(capturedBytes!);
        json.Should().Contain("myProperty");
        json.Should().Contain("test-value");
    }

    [Fact]
    public async Task SetAsync_NullKey_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = async () => await sut.SetAsync(null!, "value");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetAsync_NullValue_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = async () => await sut.SetAsync<string>("key", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SetAsync_CustomExpiration_PassesCorrectOptions()
    {
        // Arrange
        var sut = CreateSut();
        DistributedCacheEntryOptions? capturedOptions = null;
        await _distributedCache.SetAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Do<DistributedCacheEntryOptions>(o => capturedOptions = o),
            Arg.Any<CancellationToken>());

        var customDuration = TimeSpan.FromMinutes(10);

        // Act
        await sut.SetAsync("key", "value", customDuration);

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.AbsoluteExpirationRelativeToNow.Should().Be(customDuration);
    }

    [Fact]
    public async Task SetAsync_NoExpiration_UsesDefault30Minutes()
    {
        // Arrange
        var sut = CreateSut();
        DistributedCacheEntryOptions? capturedOptions = null;
        await _distributedCache.SetAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Do<DistributedCacheEntryOptions>(o => capturedOptions = o),
            Arg.Any<CancellationToken>());

        // Act
        await sut.SetAsync("key", "value");

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task RemoveAsync_ValidKey_CallsDistributedCacheRemove()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.RemoveAsync("some-key");

        // Assert
        await _distributedCache.Received(1).RemoveAsync("some-key", Arg.Any<CancellationToken>());
    }
}
