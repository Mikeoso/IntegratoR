using FluentAssertions;
using IntegratoR.OData.FO.Common.Extensions;
using IntegratoR.OData.FO.Domain.Models.Settings;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IntegratoR.OData.FO.Tests.Common.Extensions;

/// <summary>
/// Tests for <see cref="ApplicationDependencyInjection.AddODataClientFOProxy"/> verifying MediatR registration and FOSettings binding.
/// </summary>
public class ApplicationDependencyInjectionTests
{
    /// <summary>
    /// Verifies that AddODataClientFOProxy registers MediatR services so that IMediator can be resolved.
    /// </summary>
    [Fact]
    public void AddODataClientFOProxy_RegistersMediatR_WithGenericHandlers()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FOSettings:DimensionFormatName"] = "DefaultFormat"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddODataClientFOProxy(config);
        var provider = services.BuildServiceProvider();

        // Assert -- IMediator should be registered
        var mediator = provider.GetService<IMediator>();
        mediator.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that AddODataClientFOProxy correctly binds FOSettings from IConfiguration.
    /// </summary>
    [Fact]
    public void AddODataClientFOProxy_ConfiguresFOSettings()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FOSettings:DimensionFormatName"] = "DefaultFormat"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddODataClientFOProxy(config);
        var provider = services.BuildServiceProvider();

        // Assert
        var settings = provider.GetRequiredService<IOptions<FOSettings>>().Value;
        settings.DimensionFormatName.Should().Be("DefaultFormat");
    }
}
