using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Authentication;
using IntegratoR.RELion.Common.Authentication;
using IntegratoR.RELion.Common.Extensions;
using IntegratoR.RELion.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace IntegratoR.RELion.Tests.Common.Extensions;

/// <summary>
/// Tests for <see cref="ApplicationDependencyInjection"/> verifying service registrations.
/// </summary>
public class ApplicationDependencyInjectionTests
{
    private static IServiceCollection BuildServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RelionSettings:Url"] = "https://relion.test",
                ["RelionSettings:Company"] = "TestCompany"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // IAuthenticator must be registered before AddRelionClient so the
        // RelionAuthenticationHandler can be activated by the HttpClientFactory.
        var authenticator = Substitute.For<IAuthenticator>();
        authenticator.GetAccessTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Ok("test-token"));
        services.AddSingleton(authenticator);

        services.AddRelionClient(configuration);
        return services;
    }

    /// <summary>
    /// Verifies that IRelionService is registered with Scoped lifetime.
    /// </summary>
    [Fact]
    public void AddRelionClient_RegistersRelionService_AsScoped()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IRelionService));

        // Assert
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    /// <summary>
    /// Verifies that RelionAuthenticationHandler is registered with Transient lifetime.
    /// </summary>
    [Fact]
    public void AddRelionClient_RegistersAuthHandler_AsTransient()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(RelionAuthenticationHandler));

        // Assert
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Transient);
    }

    /// <summary>
    /// Verifies that IHttpClientFactory can create the "RelionApiClient" named client.
    /// </summary>
    [Fact]
    public void AddRelionClient_RegistersNamedHttpClient()
    {
        // Arrange
        var services = BuildServices();
        var provider = services.BuildServiceProvider();

        // Act
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("RelionApiClient");

        // Assert
        client.Should().NotBeNull();
    }
}
