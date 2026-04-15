using System.Net;
using FluentAssertions;
using IntegratoR.Abstractions.Interfaces.Authentication;
using IntegratoR.OData.Common.Authentication;
using IntegratoR.OData.Common.Extensions;
using IntegratoR.OData.Common.Services;
using IntegratoR.OData.Domain.Settings;
using IntegratoR.OData.Interfaces.Services;
using IntegratoR.TestKit.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace IntegratoR.OData.Tests.Common.Extensions;

/// <summary>
/// Tests for OData dependency injection registration via <see cref="ApplicationDependencyInjection"/>.
/// </summary>
public class ApplicationDependencyInjectionTests
{
    /// <summary>
    /// Verifies that settings are correctly bound from IConfiguration using the config-based overload.
    /// </summary>
    [Fact]
    public void AddODataClient_WithConfiguration_BindsSettingsFromConfigSection()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ODataSettings:Url"] = "https://test.operations.dynamics.com/data",
                ["ODataSettings:Authentication:Mode"] = "OAuth",
                ["ODataSettings:Authentication:OAuth:ClientId"] = "test-client-id",
                ["ODataSettings:Authentication:OAuth:TenantId"] = "test-tenant-id",
                ["ODataSettings:Authentication:OAuth:Resource"] = "https://test.operations.dynamics.com",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuthenticator>(Substitute.For<IAuthenticator>());

        // Act
        services.AddODataClient(config);
        var provider = services.BuildServiceProvider();

        // Assert
        var settings = provider.GetRequiredService<IOptions<ODataSettings>>().Value;
        settings.Url.Should().Be("https://test.operations.dynamics.com/data");
        settings.Authentication.Mode.Should().Be(AuthenticationMode.OAuth);
        settings.Authentication.OAuth.ClientId.Should().Be("test-client-id");
    }

    /// <summary>
    /// Verifies that ApiKey mode settings are correctly bound from IConfiguration.
    /// </summary>
    [Fact]
    public void AddODataClient_WithConfiguration_ApiKeyMode_BindsApiManagementSettings()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ODataSettings:Url"] = "https://apim.azure-api.net/d365",
                ["ODataSettings:Authentication:Mode"] = "ApiKey",
                ["ODataSettings:Authentication:ApiManagement:SubscriptionKey"] = "test-sub-key",
                ["ODataSettings:Authentication:ApiManagement:SubscriptionHeaderKey"] = "X-Custom-Key",
                ["ODataSettings:Authentication:ApiManagement:DefaultHeaders:d365foenvironment"] = "UAT",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuthenticator>(Substitute.For<IAuthenticator>());

        // Act
        services.AddODataClient(config);
        var provider = services.BuildServiceProvider();

        // Assert
        var settings = provider.GetRequiredService<IOptions<ODataSettings>>().Value;
        settings.Authentication.Mode.Should().Be(AuthenticationMode.ApiKey);
        settings.Authentication.ApiManagement.SubscriptionKey.Should().Be("test-sub-key");
        settings.Authentication.ApiManagement.SubscriptionHeaderKey.Should().Be("X-Custom-Key");
        settings.Authentication.ApiManagement.DefaultHeaders.Should().ContainKey("d365foenvironment")
            .WhoseValue.Should().Be("UAT");
    }

    /// <summary>
    /// Verifies that resilience and metadata settings are correctly bound from IConfiguration.
    /// </summary>
    [Fact]
    public void AddODataClient_WithConfiguration_BindsResilienceAndMetadataSettings()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ODataSettings:Url"] = "https://test.operations.dynamics.com/data",
                ["ODataSettings:MetadataFilePath"] = "metadata.xml",
                ["ODataSettings:Resilience:EnableRetries"] = "false",
                ["ODataSettings:Resilience:RetryCount"] = "5",
                ["ODataSettings:Resilience:UseCircuitBreaker"] = "false",
                ["ODataSettings:Resilience:CircuitBreakerThreshold"] = "10",
                ["ODataSettings:Resilience:CircuitBreakerDurationInSeconds"] = "60",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuthenticator>(Substitute.For<IAuthenticator>());

        // Act
        services.AddODataClient(config);
        var provider = services.BuildServiceProvider();

        // Assert
        var settings = provider.GetRequiredService<IOptions<ODataSettings>>().Value;
        settings.MetadataFilePath.Should().Be("metadata.xml");
        settings.Resilience.EnableRetries.Should().BeFalse();
        settings.Resilience.RetryCount.Should().Be(5);
        settings.Resilience.UseCircuitBreaker.Should().BeFalse();
        settings.Resilience.CircuitBreakerThreshold.Should().Be(10);
        settings.Resilience.CircuitBreakerDurationInSeconds.Should().Be(60);
    }

    /// <summary>
    /// Verifies that settings are correctly applied using the action-based overload.
    /// </summary>
    [Fact]
    public void AddODataClient_WithAction_BindsSettingsFromAction()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuthenticator>(Substitute.For<IAuthenticator>());

        // Act
        services.AddODataClient(options =>
        {
            options.Url = "https://action.operations.dynamics.com/data";
            options.Authentication.OAuth.ClientId = "action-client-id";
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var settings = provider.GetRequiredService<IOptions<ODataSettings>>().Value;
        settings.Url.Should().Be("https://action.operations.dynamics.com/data");
        settings.Authentication.OAuth.ClientId.Should().Be("action-client-id");
    }

    /// <summary>
    /// Verifies that ODataAuthenticationHandler is registered as Transient.
    /// </summary>
    [Fact]
    public void AddODataClient_RegistersAuthenticationHandlerAsTransient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuthenticator>(Substitute.For<IAuthenticator>());
        services.AddODataClient(options => options.Url = "https://test.example.com");

        // Act
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ODataAuthenticationHandler));

        // Assert
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Transient);
    }

    /// <summary>
    /// Verifies that ODataMetadataProvider is registered as Singleton.
    /// </summary>
    [Fact]
    public void AddODataClient_RegistersMetadataProviderAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuthenticator>(Substitute.For<IAuthenticator>());
        services.AddODataClient(options => options.Url = "https://test.example.com");

        // Act
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ODataMetadataProvider));

        // Assert
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    /// <summary>
    /// Verifies that a named HttpClient ("ODataClient") is registered in the service collection.
    /// </summary>
    [Fact]
    public void AddODataClient_RegistersNamedHttpClient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuthenticator>(Substitute.For<IAuthenticator>());
        services.AddODataClient(options => options.Url = "https://test.example.com");

        // Act
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("ODataClient");

        // Assert
        client.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that IODataClientAdapter is registered as Singleton.
    /// </summary>
    [Fact]
    public void AddODataClient_RegistersODataClientAdapterAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuthenticator>(Substitute.For<IAuthenticator>());
        services.AddODataClient(options => options.Url = "https://test.example.com");

        // Act
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IODataClientAdapter));

        // Assert
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    /// <summary>
    /// Verifies that the named ODataClient HttpClient has its BaseAddress normalised with a
    /// trailing slash regardless of what the consumer writes in configuration. Without this
    /// normalisation, <see cref="HttpClient.BaseAddress"/> silently drops preceding path segments
    /// (e.g. <c>/fo</c>) when a relative request URI starts with <c>/</c>, which caused 404s
    /// against APIM in production. Regression coverage for PR #79 follow-up.
    /// </summary>
    [Theory]
    [InlineData("https://host/fo", "https://host/fo/")]
    [InlineData("https://host/fo/", "https://host/fo/")]
    [InlineData("https://host/", "https://host/")]
    [InlineData("https://host", "https://host/")]
    public void AddODataClient_NormalisesBaseAddress_WhenUrlLacksTrailingSlash(
        string configuredUrl,
        string expectedBaseAddress)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuthenticator>(Substitute.For<IAuthenticator>());
        services.AddODataClient(options => options.Url = configuredUrl);

        var provider = services.BuildServiceProvider();

        // Act
        var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("ODataClient");

        // Assert
        httpClient.BaseAddress.Should().NotBeNull();
        httpClient.BaseAddress!.AbsoluteUri.Should().Be(expectedBaseAddress);
        httpClient.BaseAddress!.AbsoluteUri.Should().EndWith("/");
    }

    /// <summary>
    /// Proves the fix end-to-end: when the HttpClient issues a relative request, the preceding
    /// path segment from the configured URL (e.g. <c>/fo</c>) is preserved in the composed
    /// absolute URI. Without the trailing-slash normalisation, the relative URI would have
    /// replaced the path segment entirely. This is the regression test that would have caught
    /// the live sandbox failure where <c>/fo</c> was silently dropped against APIM.
    /// </summary>
    [Fact]
    public async Task AddODataClient_PreservesPathSegment_WhenComposingRelativeRequestUri()
    {
        // Arrange
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.Queue(HttpStatusCode.OK, "{}");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuthenticator>(Substitute.For<IAuthenticator>());
        services.AddODataClient(options =>
        {
            options.Url = "https://lbbw-im-api-management.azure-api.net/fo";
            options.Authentication.Mode = AuthenticationMode.ApiKey;
            options.Authentication.ApiManagement.SubscriptionKey = "test-key";
            options.Authentication.ApiManagement.SubscriptionHeaderKey = "Ocp-Apim-Subscription-Key";
            options.Resilience.EnableRetries = false;
            options.Resilience.UseCircuitBreaker = false;
        });

        // Plug the fake as the terminal primary handler for the named client so the request
        // chain is: ODataAuthenticationHandler -> Polly (no-op) -> FakeHttpMessageHandler.
        services.AddHttpClient("ODataClient").ConfigurePrimaryHttpMessageHandler(() => fakeHandler);

        var provider = services.BuildServiceProvider();
        var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("ODataClient");

        // Act - issue a GET with a relative URI matching what PanoramicData emits for entity sets.
        _ = await httpClient.GetAsync("LedgerJournalHeaders", CancellationToken.None);

        // Assert - the /fo segment must be preserved.
        fakeHandler.SentRequests.Should().HaveCount(1);
        fakeHandler.SentRequests[0].RequestUri.Should().NotBeNull();
        fakeHandler.SentRequests[0].RequestUri!.AbsoluteUri
            .Should().Be("https://lbbw-im-api-management.azure-api.net/fo/LedgerJournalHeaders");
    }

    /// <summary>
    /// Sanity check that BOTH the HttpClient BaseAddress AND the <see cref="ODataClientOptions"/>
    /// BaseUrl that PanoramicData uses land on the same normalised, trailing-slash string.
    /// Pins that we did not forget either side of the pair.
    /// </summary>
    [Fact]
    public void AddODataClient_ODataClientOptionsBaseUrl_MatchesNormalisedHttpClientBaseAddress()
    {
        // Arrange
        const string configuredUrl = "https://host/fo";
        string expectedNormalised = ApplicationDependencyInjection.NormaliseBaseUrl(configuredUrl);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuthenticator>(Substitute.For<IAuthenticator>());
        services.AddODataClient(options => options.Url = configuredUrl);

        var provider = services.BuildServiceProvider();

        // Force the ODataClient singleton to instantiate so its registration factory executes.
        // That factory reads the same NormaliseBaseUrl helper and feeds the result into
        // ODataClientOptions.BaseUrl — so by pinning the helper output, we pin both sides.
        _ = provider.GetRequiredService<PanoramicData.OData.Client.ODataClient>();

        var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("ODataClient");

        // Assert
        expectedNormalised.Should().Be("https://host/fo/");
        httpClient.BaseAddress!.AbsoluteUri.Should().Be(expectedNormalised);
    }

    /// <summary>
    /// Pins that <see cref="ApplicationDependencyInjection.NormaliseBaseUrl"/> throws an
    /// <see cref="ArgumentException"/> with a clear message when <c>ODataSettings.Url</c> is
    /// missing or whitespace. Without the guard, <c>new Uri("/")</c> throws
    /// <see cref="UriFormatException"/> at DI resolution — a misleading error for an operator
    /// who simply forgot to set <c>Url</c> in configuration.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormaliseBaseUrl_NullOrWhitespace_ThrowsArgumentException(string? url)
    {
        Action act = () => ApplicationDependencyInjection.NormaliseBaseUrl(url!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ODataSettings.Url must be set*");
    }

    /// <summary>
    /// Verifies that BaseAddress normalisation is local to the HttpClient wiring and does not
    /// mutate the public <see cref="ODataSettings.Url"/>. Consumers reading
    /// <see cref="IOptions{TOptions}"/> must see exactly what they wrote.
    /// </summary>
    [Fact]
    public void AddODataClient_DoesNotMutateSettingsUrl()
    {
        // Arrange
        const string configuredUrl = "https://host/fo";

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuthenticator>(Substitute.For<IAuthenticator>());
        services.AddODataClient(options => options.Url = configuredUrl);

        var provider = services.BuildServiceProvider();

        // Force the HttpClient factory to run its configuration action so any mutation would
        // have already happened by the time we read IOptions.Value.
        _ = provider.GetRequiredService<IHttpClientFactory>().CreateClient("ODataClient");

        // Act
        var settings = provider.GetRequiredService<IOptions<ODataSettings>>().Value;

        // Assert
        settings.Url.Should().Be(configuredUrl);
        settings.Url.Should().NotEndWith("/");
    }
}
