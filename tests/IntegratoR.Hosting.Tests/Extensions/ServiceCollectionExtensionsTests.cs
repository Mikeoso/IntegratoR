using FluentAssertions;
using IntegratoR.Abstractions.Interfaces.Authentication;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.Domain.Settings;
using IntegratoR.OData.FO.Domain.Models.Settings;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IntegratoR.Hosting.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    private static IConfiguration CreateMinimalConfiguration()
    {
        var configData = new Dictionary<string, string?>
        {
            ["ODataSettings:Url"] = "https://test.operations.dynamics.com/data",
            ["ODataSettings:ClientId"] = "test-client-id",
            ["ODataSettings:ClientSecret"] = "test-secret",
            ["ODataSettings:TenantId"] = "test-tenant",
            ["ODataSettings:Resource"] = "https://test.operations.dynamics.com",
            ["FOSettings:DimensionFormatName"] = "TestFormat"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    [Fact]
    public void AddIntegratoR_SimpleOverload_RegistersCoreServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        IConfiguration configuration = CreateMinimalConfiguration();

        // Act
        services.AddIntegratoR(configuration);

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<IMediator>().Should().NotBeNull();
        provider.GetService<ICacheService>().Should().NotBeNull();
        provider.GetService<IAuthenticator>().Should().NotBeNull();
    }

    [Fact]
    public void AddIntegratoR_ConfigureOData_AppliesPostConfigureOverrides()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        IConfiguration configuration = CreateMinimalConfiguration();

        // Act
        services.AddIntegratoR(configuration, integrator =>
        {
            integrator.ConfigureOData(odata => odata.Timeout = 300);
        });

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();
        ODataSettings settings = provider.GetRequiredService<IOptions<ODataSettings>>().Value;

        settings.Timeout.Should().Be(300);
    }

    [Fact]
    public void AddIntegratoR_ConfigureFO_AppliesPostConfigureOverrides()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        IConfiguration configuration = CreateMinimalConfiguration();

        // Act
        services.AddIntegratoR(configuration, integrator =>
        {
            integrator.ConfigureFO(fo => fo.DimensionFormatName = "CustomFormat");
        });

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();
        FOSettings settings = provider.GetRequiredService<IOptions<FOSettings>>().Value;

        settings.DimensionFormatName.Should().Be("CustomFormat");
    }

    [Fact]
    public void AddIntegratoR_AddConsumerHandlers_RegistersValidatorsFromAssembly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        IConfiguration configuration = CreateMinimalConfiguration();

        // Act
        services.AddIntegratoR(configuration, integrator =>
        {
            integrator.AddConsumerHandlers(typeof(ServiceCollectionExtensionsTests).Assembly);
        });

        // Assert — MediatR should be registered with an additional assembly scan
        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetService<IMediator>().Should().NotBeNull();
    }

    [Fact]
    public void AddIntegratoR_ODataSettings_BoundFromConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        IConfiguration configuration = CreateMinimalConfiguration();

        // Act
        services.AddIntegratoR(configuration);

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();
        ODataSettings settings = provider.GetRequiredService<IOptions<ODataSettings>>().Value;

        settings.Url.Should().Be("https://test.operations.dynamics.com/data");
        settings.ClientId.Should().Be("test-client-id");
    }
}
