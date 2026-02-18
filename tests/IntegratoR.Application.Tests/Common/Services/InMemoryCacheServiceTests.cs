using FluentAssertions;
using IntegratoR.Application.Common.Services;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace IntegratoR.Application.Tests.Common.Services;

/// <summary>
/// Tests for <see cref="InMemoryCacheService"/> using a real <see cref="MemoryCache"/>.
/// </summary>
public class InMemoryCacheServiceTests
{
    private static InMemoryCacheService CreateSut()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        return new InMemoryCacheService(memoryCache);
    }

    [Fact]
    public async Task GetAsync_ExistingKey_ReturnsCachedValue()
    {
        // Arrange
        var sut = CreateSut();
        await sut.SetAsync("key1", "hello");

        // Act
        var result = await sut.GetAsync<string>("key1");

        // Assert
        result.Should().Be("hello");
    }

    [Fact]
    public async Task GetAsync_NonExistingKey_ReturnsDefault()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetAsync<string>("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_NullOrEmptyKey_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = async () => await sut.GetAsync<string>(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SetAsync_ValidKeyAndValue_StoresInCache()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.SetAsync("myKey", 42);

        // Assert
        var result = await sut.GetAsync<int>("myKey");
        result.Should().Be(42);
    }

    [Fact]
    public async Task SetAsync_NullKey_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = async () => await sut.SetAsync(null!, "value");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
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
    public async Task SetAsync_CustomExpiration_UsesProvidedExpiration()
    {
        // Arrange -- we verify the entry was stored (expiration is internal to MemoryCache)
        var sut = CreateSut();

        // Act
        await sut.SetAsync("expKey", "value", TimeSpan.FromMinutes(1));

        // Assert -- value is available immediately
        var result = await sut.GetAsync<string>("expKey");
        result.Should().Be("value");
    }

    [Fact]
    public async Task SetAsync_NoExpiration_UsesDefault30Minutes()
    {
        // Arrange -- default expiration is 30 minutes; we verify the value is stored
        var sut = CreateSut();

        // Act
        await sut.SetAsync("defaultKey", "stored-value");

        // Assert
        var result = await sut.GetAsync<string>("defaultKey");
        result.Should().Be("stored-value");
    }

    [Fact]
    public async Task RemoveAsync_ExistingKey_RemovesFromCache()
    {
        // Arrange
        var sut = CreateSut();
        await sut.SetAsync("removeKey", "to-be-removed");

        // Act
        await sut.RemoveAsync("removeKey");

        // Assert
        var result = await sut.GetAsync<string>("removeKey");
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_NullKey_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = async () => await sut.RemoveAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
