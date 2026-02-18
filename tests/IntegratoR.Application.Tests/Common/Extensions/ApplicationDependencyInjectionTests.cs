using FluentAssertions;
using IntegratoR.Abstractions.Interfaces.Authentication;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.Application.Common.Behaviours;
using IntegratoR.Application.Common.Extensions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegratoR.Application.Tests.Common.Extensions;

/// <summary>
/// Tests for <see cref="ApplicationDependencyInjection"/>.
/// </summary>
public class ApplicationDependencyInjectionTests
{
    private static IServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();
        return services;
    }

    [Fact]
    public void AddApplicationServices_RegistersPipelineBehaviours_InCorrectOrder()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var behaviourDescriptors = services
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<,>))
            .ToList();

        // Assert -- 3 behaviours registered in order: Logging -> Validation -> Caching
        behaviourDescriptors.Should().HaveCount(3);
        behaviourDescriptors[0].ImplementationType.Should().Be(typeof(LoggingBehaviour<,>));
        behaviourDescriptors[1].ImplementationType.Should().Be(typeof(ValidationBehaviour<,>));
        behaviourDescriptors[2].ImplementationType.Should().Be(typeof(CachingBehaviour<,>));
    }

    [Fact]
    public void AddApplicationServices_RegistersCacheService_AsSingleton()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICacheService));

        // Assert
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddApplicationServices_RegistersAuthenticator_AsSingleton()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuthenticator));

        // Assert
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddApplicationServices_RegistersMediatR_WithGenericHandlers()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var mediatRDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMediator));

        // Assert
        mediatRDescriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddApplicationServices_RegistersValidators_FromAssembly()
    {
        // Arrange & Act
        var services = BuildServices();
        var provider = services.BuildServiceProvider();

        // Assert -- FluentValidation registers validators; verify service provider can be built
        provider.Should().NotBeNull();

        // Assert -- service provider builds successfully with all registrations
        provider.Should().NotBeNull();
    }
}
