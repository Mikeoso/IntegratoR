using FluentAssertions;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Application.Common.Authentication;
using IntegratoR.TestKit.Assertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Identity.Client;
using NSubstitute;
using Xunit;

namespace IntegratoR.Application.Tests.Common.Authentication;

/// <summary>
/// Tests for <see cref="OAuthAuthenticator"/>.
/// </summary>
/// <remarks>
/// The MSAL network path is exercised via the internal test-seam constructor, which substitutes the
/// confidential-client app factory and the token-acquisition delegate so no live Azure AD is needed.
/// The static app-cache inside <see cref="OAuthAuthenticator"/> persists across tests, so every test
/// here uses a UNIQUE clientId (and matching cache key) to avoid cross-test bleed.
/// </remarks>
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
        var result = await sut.GetAccessTokenAsync("client-id", "secret", "tenant-id", "https://resource", CancellationToken.None);

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
        await sut.GetAccessTokenAsync(clientId, "secret", "tenant", resource, CancellationToken.None);

        // Assert
        _memoryCache.Received(1).TryGetValue(expectedKey, out Arg.Any<object?>());
    }

    [Fact]
    public async Task GetAccessTokenAsync_MsalClientException_ReturnsFailWithMsalCode_DoesNotThrow()
    {
        // Arrange -- the acquirer throws a client-side MSAL error; the cache misses.
        const string clientId = "msal-client-error-client";
        var app = Substitute.For<IConfidentialClientApplication>();
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object?>()).Returns(false);

        var sut = new OAuthAuthenticator(
            _memoryCache,
            (_, _, _, _) => app,
            (_, _, _) => throw new MsalClientException("some_client_error", "bad config"));

        // Act
        Func<Task> act = async () =>
        {
            var result = await sut.GetAccessTokenAsync(clientId, "secret", "tenant", "https://resource", CancellationToken.None);

            // Assert (inside the act delegate so we both prove no-throw AND inspect the result)
            result.Should().BeFailed();
            result.Should().HaveErrorCode("Auth.Msal.some_client_error");
            result.Should().HaveErrorType(ErrorType.Failure);
        };

        // Assert -- the MSAL exception is mapped to a failed Result, never propagated.
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetAccessTokenAsync_NoCachedToken_SuccessPath_CachesAndReturnsToken()
    {
        // Arrange -- real MemoryCache so the success path actually caches; unique clientId.
        const string clientId = "success-path-unique-client";
        const string resource = "https://success-resource";
        using var realCache = new MemoryCache(new MemoryCacheOptions());
        var app = Substitute.For<IConfidentialClientApplication>();

        var acquirerCallCount = 0;
        var sut = new OAuthAuthenticator(
            realCache,
            (_, _, _, _) => app,
            (_, _, _) =>
            {
                acquirerCallCount++;
                return Task.FromResult(new AcquiredToken("new-token", DateTimeOffset.UtcNow.AddHours(1)));
            });

        // Act -- first call acquires and caches.
        var first = await sut.GetAccessTokenAsync(clientId, "secret", "tenant", resource, CancellationToken.None);

        // Assert
        first.Should().BeSuccessful();
        first.Should().HaveValue("new-token");

        // Act -- second call must be served from the cache without re-invoking the acquirer.
        var second = await sut.GetAccessTokenAsync(clientId, "secret", "tenant", resource, CancellationToken.None);

        // Assert
        second.Should().BeSuccessful();
        second.Should().HaveValue("new-token");
        acquirerCallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAccessTokenAsync_TwoCacheMisses_ReuseSameConfidentialClientAppInstance()
    {
        // Arrange -- token-cache misses both times, so only the static app-cache can prevent a
        // rebuild. A unique clientId|tenantId|resource keeps the static cache key isolated.
        const string clientId = "app-cache-reuse-unique-client";
        const string tenantId = "app-cache-reuse-tenant";
        const string resource = "https://app-cache-reuse-resource";

        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object?>()).Returns(false);

        var app = Substitute.For<IConfidentialClientApplication>();
        var appFactoryCallCount = 0;

        var sut = new OAuthAuthenticator(
            _memoryCache,
            (_, _, _, _) =>
            {
                appFactoryCallCount++;
                return app;
            },
            (_, _, _) => Task.FromResult(new AcquiredToken("token", DateTimeOffset.UtcNow.AddHours(1))));

        // Act -- two calls with identical client/tenant/resource.
        await sut.GetAccessTokenAsync(clientId, "secret", tenantId, resource, CancellationToken.None);
        await sut.GetAccessTokenAsync(clientId, "secret", tenantId, resource, CancellationToken.None);

        // Assert -- the confidential-client app was built exactly once and reused.
        appFactoryCallCount.Should().Be(1);
    }
}
