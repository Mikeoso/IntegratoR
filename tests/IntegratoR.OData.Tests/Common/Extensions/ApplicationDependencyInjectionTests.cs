using FluentAssertions;
using IntegratoR.Abstractions.Interfaces.Authentication;
using IntegratoR.OData.Common.Authentication;
using IntegratoR.OData.Common.Extensions;
using IntegratoR.OData.Common.Services;
using IntegratoR.OData.Domain.Settings;
using IntegratoR.OData.Interfaces.Services;
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
        settings.Authentication.OAuth.ClientId.Should().Be("test-client-id");
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
}
