using FluentAssertions;
using IntegratoR.Application.Common.Authentication;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using Xunit;

namespace IntegratoR.Application.Tests.Common.Authentication;

/// <summary>
/// Tests for <see cref="OAuthAuthenticator"/>.
/// </summary>
public class OAuthAuthenticatorTests
{
    private readonly IMemoryCache _memoryCache = Substitute.For<IMemoryCache>();

    [Fact]
    public async Task GetAccessTokenAsync_CachedToken_ReturnsCachedValue()
    {
        // Arrange
        var sut = new OAuthAuthenticator(_memoryCache);
        object? cachedToken = "cached-access-token";
        _memoryCache.TryGetValue("AccessToken-client-id-https://resource", out Arg.Any<object?>())
            .Returns(x =>
            {
                x[1] = cachedToken;
                return true;
            });

        // Act
        var result = await sut.GetAccessTokenAsync("client-id", "secret", "tenant-id", "https://resource");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("cached-access-token");
    }

    [Fact]
    public async Task GetAccessTokenAsync_CacheKeyFormat_IncludesClientIdAndResource()
    {
        // Arrange
        var sut = new OAuthAuthenticator(_memoryCache);
        var clientId = "my-client-id";
        var resource = "https://my-resource";
        var expectedKey = $"AccessToken-{clientId}-{resource}";

        object? cachedToken = "some-token";
        _memoryCache.TryGetValue(expectedKey, out Arg.Any<object?>())
            .Returns(x =>
            {
                x[1] = cachedToken;
                return true;
            });

        // Act
        await sut.GetAccessTokenAsync(clientId, "secret", "tenant", resource);

        // Assert
        _memoryCache.Received(1).TryGetValue(expectedKey, out Arg.Any<object?>());
    }

    [Fact(Skip = "Requires MSAL integration -- live Azure AD not available in unit tests")]
    public async Task GetAccessTokenAsync_NoCachedToken_AcquiresNewToken()
    {
        // This test requires a real MSAL connection and is intentionally skipped in unit tests.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Requires MSAL integration to trigger MsalServiceException -- not unit-testable without MSAL mocking")]
    public async Task GetAccessTokenAsync_MsalServiceException_ReturnsIntegrationError()
    {
        // This test requires MSAL internals to be mockable.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Requires MSAL integration to trigger MsalException -- not unit-testable without MSAL mocking")]
    public async Task GetAccessTokenAsync_MsalException_ErrorCodeContainsMsalErrorCode()
    {
        // This test requires MSAL internals to be mockable.
        await Task.CompletedTask;
    }
}
