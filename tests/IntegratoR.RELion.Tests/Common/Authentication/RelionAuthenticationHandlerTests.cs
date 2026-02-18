using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Authentication;
using IntegratoR.RELion.Common.Authentication;
using IntegratoR.RELion.Domain.Settings;
using IntegratoR.TestKit.Fakes;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using Xunit;

namespace IntegratoR.RELion.Tests.Common.Authentication;

/// <summary>
/// Tests for <see cref="RelionAuthenticationHandler"/> covering OAuth and ApiKey authentication modes.
/// </summary>
public class RelionAuthenticationHandlerTests
{
    /// <summary>
    /// Verifies that in OAuth mode with a successful token, the Authorization header is set to Bearer.
    /// </summary>
    [Fact]
    public async Task SendAsync_OAuthMode_SuccessfulToken_AddsBearerHeader()
    {
        // Arrange
        var settings = Options.Create(new RelionSettings
        {
            Url = "https://relion.test",
            AuthMode = RelionAuthMode.OAuth,
            ClientId = "client-id",
            ClientSecret = "client-secret",
            TenantId = "tenant-id",
            Resource = "resource"
        });
        var authenticator = Substitute.For<IAuthenticator>();
        authenticator.GetAccessTokenAsync("client-id", "client-secret", "tenant-id", "resource")
            .Returns(Result.Ok("test-token"));

        var innerHandler = new FakeHttpMessageHandler();
        innerHandler.Queue(HttpStatusCode.OK);

        var handler = new RelionAuthenticationHandler(settings, authenticator)
        {
            InnerHandler = innerHandler
        };
        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://relion.test/api/test"));

        // Act
        var response = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        innerHandler.SentRequests.Should().HaveCount(1);
        innerHandler.SentRequests[0].Headers.Authorization.Should().NotBeNull();
        innerHandler.SentRequests[0].Headers.Authorization!.Scheme.Should().Be("Bearer");
        innerHandler.SentRequests[0].Headers.Authorization!.Parameter.Should().Be("test-token");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies that in OAuth mode with a failed token result, a 401 Unauthorized response is returned.
    /// </summary>
    [Fact]
    public async Task SendAsync_OAuthMode_FailedToken_Returns401Unauthorized()
    {
        // Arrange
        var settings = Options.Create(new RelionSettings
        {
            Url = "https://relion.test",
            AuthMode = RelionAuthMode.OAuth,
            ClientId = "client-id",
            ClientSecret = "client-secret",
            TenantId = "tenant-id",
            Resource = "resource"
        });
        var authenticator = Substitute.For<IAuthenticator>();
        authenticator.GetAccessTokenAsync("client-id", "client-secret", "tenant-id", "resource")
            .Returns(Result.Fail<string>("Token acquisition failed"));

        var innerHandler = new FakeHttpMessageHandler();
        var handler = new RelionAuthenticationHandler(settings, authenticator)
        {
            InnerHandler = innerHandler
        };
        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://relion.test/api/test"));

        // Act
        var response = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.ReasonPhrase.Should().Contain("Relion OAuth token");
    }

    /// <summary>
    /// Verifies that in ApiKey mode, the subscription key header is added to the request.
    /// </summary>
    [Fact]
    public async Task SendAsync_ApiKeyMode_AddsSubscriptionKeyHeader()
    {
        // Arrange
        var settings = Options.Create(new RelionSettings
        {
            Url = "https://relion.test",
            AuthMode = RelionAuthMode.ApiKey,
            SubscriptionHeaderKey = "Ocp-Apim-Subscription-Key",
            SubscriptionKey = "my-subscription-key"
        });
        var authenticator = Substitute.For<IAuthenticator>();

        var innerHandler = new FakeHttpMessageHandler();
        innerHandler.Queue(HttpStatusCode.OK);

        var handler = new RelionAuthenticationHandler(settings, authenticator)
        {
            InnerHandler = innerHandler
        };
        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://relion.test/api/test"));

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        innerHandler.SentRequests.Should().HaveCount(1);
        innerHandler.SentRequests[0].Headers.Should().Contain(h =>
            h.Key == "Ocp-Apim-Subscription-Key" &&
            h.Value.Contains("my-subscription-key"));
    }

    /// <summary>
    /// Verifies that in OAuth mode, the authenticator is called with the Relion-specific settings values.
    /// </summary>
    [Fact]
    public async Task SendAsync_OAuthMode_UsesRelionSpecificSettings()
    {
        // Arrange
        var settings = Options.Create(new RelionSettings
        {
            Url = "https://relion.test",
            AuthMode = RelionAuthMode.OAuth,
            ClientId = "relion-client-id",
            ClientSecret = "relion-client-secret",
            TenantId = "relion-tenant-id",
            Resource = "relion-resource"
        });
        var authenticator = Substitute.For<IAuthenticator>();
        authenticator.GetAccessTokenAsync(
                "relion-client-id",
                "relion-client-secret",
                "relion-tenant-id",
                "relion-resource")
            .Returns(Result.Ok("relion-access-token"));

        var innerHandler = new FakeHttpMessageHandler();
        innerHandler.Queue(HttpStatusCode.OK);

        var handler = new RelionAuthenticationHandler(settings, authenticator)
        {
            InnerHandler = innerHandler
        };
        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://relion.test/api/test"));

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        await authenticator.Received(1).GetAccessTokenAsync(
            "relion-client-id",
            "relion-client-secret",
            "relion-tenant-id",
            "relion-resource");
    }
}
