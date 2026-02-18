using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Authentication;
using IntegratoR.OData.Common.Authentication;
using IntegratoR.OData.Domain.Settings;
using IntegratoR.TestKit.Fakes;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using Xunit;

namespace IntegratoR.OData.Tests.Common.Authentication;

/// <summary>
/// Tests for <see cref="ODataAuthenticationHandler"/> covering OAuth and API key authentication modes.
/// </summary>
public class ODataAuthenticationHandlerTests
{
    private readonly IAuthenticator _authenticator;
    private readonly FakeHttpMessageHandler _fakeHandler;

    /// <summary>
    /// Initialises a new instance with mock authenticator and fake inner handler.
    /// </summary>
    public ODataAuthenticationHandlerTests()
    {
        _authenticator = Substitute.For<IAuthenticator>();
        _fakeHandler = new FakeHttpMessageHandler();
    }

    private HttpMessageInvoker CreateInvoker(ODataSettings settings)
    {
        var options = Options.Create(settings);
        var handler = new ODataAuthenticationHandler(options, _authenticator)
        {
            InnerHandler = _fakeHandler
        };
        return new HttpMessageInvoker(handler);
    }

    /// <summary>
    /// Verifies that a successful OAuth token is added as a Bearer Authorization header.
    /// </summary>
    [Fact]
    public async Task SendAsync_OAuthMode_SuccessfulToken_AddsBearerHeader()
    {
        // Arrange
        const string token = "test-token";
        _authenticator.GetAccessTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Ok(token));

        _fakeHandler.Queue(HttpStatusCode.OK);

        var settings = new ODataSettings { AuthMode = ODataAuthMode.OAuth };
        var invoker = CreateInvoker(settings);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        _fakeHandler.SentRequests.Should().HaveCount(1);
        _fakeHandler.SentRequests[0].Headers.Authorization.Should().NotBeNull();
        _fakeHandler.SentRequests[0].Headers.Authorization!.Scheme.Should().Be("Bearer");
        _fakeHandler.SentRequests[0].Headers.Authorization!.Parameter.Should().Be(token);
    }

    /// <summary>
    /// Verifies that a failed OAuth token acquisition returns a 401 response.
    /// </summary>
    [Fact]
    public async Task SendAsync_OAuthMode_FailedToken_Returns401Unauthorized()
    {
        // Arrange
        var error = new IntegrationError("Auth.Failed", "Token acquisition failed", ErrorType.Failure);
        _authenticator.GetAccessTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Fail<string>(error));

        var settings = new ODataSettings { AuthMode = ODataAuthMode.OAuth };
        var invoker = CreateInvoker(settings);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
        var response = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.ReasonPhrase.Should().Contain("Token acquisition failed");
    }

    /// <summary>
    /// Verifies that a failed OAuth token acquisition short-circuits the pipeline (inner handler not called).
    /// </summary>
    [Fact]
    public async Task SendAsync_OAuthMode_FailedToken_DoesNotCallInnerHandler()
    {
        // Arrange
        var error = new IntegrationError("Auth.Failed", "Token acquisition failed", ErrorType.Failure);
        _authenticator.GetAccessTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Fail<string>(error));

        var settings = new ODataSettings { AuthMode = ODataAuthMode.OAuth };
        var invoker = CreateInvoker(settings);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        _fakeHandler.SentRequests.Count.Should().Be(0);
    }

    /// <summary>
    /// Verifies that ApiKey mode adds the subscription key header to requests.
    /// </summary>
    [Fact]
    public async Task SendAsync_ApiKeyMode_AddsSubscriptionKeyHeader()
    {
        // Arrange
        const string subscriptionKey = "my-subscription-key";
        const string subscriptionHeaderKey = "Ocp-Apim-Subscription-Key";

        _fakeHandler.Queue(HttpStatusCode.OK);

        var settings = new ODataSettings
        {
            AuthMode = ODataAuthMode.ApiKey,
            SubscriptionKey = subscriptionKey,
            SubscriptionHeaderKey = subscriptionHeaderKey
        };
        var invoker = CreateInvoker(settings);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        _fakeHandler.SentRequests.Should().HaveCount(1);
        _fakeHandler.SentRequests[0].Headers.Should().ContainKey(subscriptionHeaderKey);
        _fakeHandler.SentRequests[0].Headers.GetValues(subscriptionHeaderKey).Should().Contain(subscriptionKey);
    }

    /// <summary>
    /// Verifies that ApiKey mode adds all default headers from ODataSettings.DefaultHeaders.
    /// </summary>
    [Fact]
    public async Task SendAsync_ApiKeyMode_AddsDefaultHeaders()
    {
        // Arrange
        _fakeHandler.Queue(HttpStatusCode.OK);

        var settings = new ODataSettings
        {
            AuthMode = ODataAuthMode.ApiKey,
            SubscriptionKey = "test-key",
            SubscriptionHeaderKey = "Ocp-Apim-Subscription-Key",
            DefaultHeaders = new Dictionary<string, string>
            {
                ["X-Custom-Header"] = "custom-value",
                ["X-Correlation-Id"] = "correlation-123"
            }
        };
        var invoker = CreateInvoker(settings);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        _fakeHandler.SentRequests.Should().HaveCount(1);
        var sentRequest = _fakeHandler.SentRequests[0];
        sentRequest.Headers.Should().ContainKey("X-Custom-Header");
        sentRequest.Headers.GetValues("X-Custom-Header").Should().Contain("custom-value");
        sentRequest.Headers.Should().ContainKey("X-Correlation-Id");
        sentRequest.Headers.GetValues("X-Correlation-Id").Should().Contain("correlation-123");
    }

    /// <summary>
    /// Verifies that OAuth mode passes the exact credential values to IAuthenticator.
    /// </summary>
    [Fact]
    public async Task SendAsync_OAuthMode_PassesCorrectCredentials()
    {
        // Arrange
        const string clientId = "test-client-id";
        const string clientSecret = "test-client-secret";
        const string tenantId = "test-tenant-id";
        const string resource = "https://test.operations.dynamics.com";

        _authenticator.GetAccessTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Ok("any-token"));
        _fakeHandler.Queue(HttpStatusCode.OK);

        var settings = new ODataSettings
        {
            AuthMode = ODataAuthMode.OAuth,
            ClientId = clientId,
            ClientSecret = clientSecret,
            TenantId = tenantId,
            Resource = resource
        };
        var invoker = CreateInvoker(settings);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        await _authenticator.Received(1).GetAccessTokenAsync(clientId, clientSecret, tenantId, resource);
    }
}
