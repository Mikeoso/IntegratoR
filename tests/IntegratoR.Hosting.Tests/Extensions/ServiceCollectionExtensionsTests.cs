using FluentAssertions;
using FluentResults;
using FluentValidation;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Authentication;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.Domain.Settings;
using IntegratoR.OData.FO.Domain.Models.Settings;
using MediatR;
using Microsoft.DurableTask.Converters;
using Microsoft.DurableTask.Worker;
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
            ["ODataSettings:Authentication:Mode"] = "OAuth",
            ["ODataSettings:Authentication:OAuth:ClientId"] = "test-client-id",
            ["ODataSettings:Authentication:OAuth:ClientSecret"] = "test-secret",
            ["ODataSettings:Authentication:OAuth:TenantId"] = "test-tenant",
            ["ODataSettings:Authentication:OAuth:Resource"] = "https://test.operations.dynamics.com",
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

        // Assert — the dummy validator defined in this assembly should be resolvable
        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetService<IValidator<DummyModel>>().Should().NotBeNull()
            .And.BeOfType<DummyModelValidator>();
    }

    [Fact]
    public void AddIntegratoR_ConfigureODataCalledTwice_ComposesOverrides()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        IConfiguration configuration = CreateMinimalConfiguration();

        // Act
        services.AddIntegratoR(configuration, integrator =>
        {
            integrator
                .ConfigureOData(odata => odata.Timeout = 300)
                .ConfigureOData(odata => odata.Resilience.RetryCount = 5);
        });

        // Assert — both overrides should be applied
        ServiceProvider provider = services.BuildServiceProvider();
        ODataSettings settings = provider.GetRequiredService<IOptions<ODataSettings>>().Value;

        settings.Timeout.Should().Be(300);
        settings.Resilience.RetryCount.Should().Be(5);
    }

    /// <summary>
    /// Verifies that AddIntegratoR registers a DurableTaskWorkerOptions configurator that
    /// installs a JsonDataConverter — so consumers using Durable Functions get Result&lt;T&gt;
    /// round-tripping for free without having to copy boilerplate into Program.cs.
    /// </summary>
    [Fact]
    public void AddIntegratoR_RegistersDurableTaskWorkerOptionsWithJsonDataConverter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        IConfiguration configuration = CreateMinimalConfiguration();

        // Act
        services.AddIntegratoR(configuration);

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();
        DurableTaskWorkerOptions options = provider.GetRequiredService<IOptions<DurableTaskWorkerOptions>>().Value;

        options.DataConverter.Should().NotBeNull();
        options.DataConverter.Should().BeOfType<JsonDataConverter>();
    }

    /// <summary>
    /// Verifies end-to-end that the DataConverter installed by AddIntegratoR can round-trip
    /// a failed Result&lt;T&gt; through serialisation — proving the Result converters are wired
    /// correctly into the JsonSerializerOptions handed to JsonDataConverter.
    /// </summary>
    [Fact]
    public void AddIntegratoR_DurableTaskDataConverter_RoundTripsResultWithIntegrationError()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        IConfiguration configuration = CreateMinimalConfiguration();
        services.AddIntegratoR(configuration);

        ServiceProvider provider = services.BuildServiceProvider();
        DurableTaskWorkerOptions options = provider.GetRequiredService<IOptions<DurableTaskWorkerOptions>>().Value;

        var error = new IntegrationError("Activity.Failed", "Activity failed.", ErrorType.Failure);
        Result<string> original = Result.Fail<string>(error);

        // Act
        string? serialised = options.DataConverter.Serialize(original);
        Result<string>? roundTripped = options.DataConverter.Deserialize(serialised, typeof(Result<string>)) as Result<string>;

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsFailed.Should().BeTrue();
        IntegrationError reconstructed = (IntegrationError)roundTripped.Errors[0];
        reconstructed.Code.Should().Be("Activity.Failed");
        reconstructed.Type.Should().Be(ErrorType.Failure);
    }

    /// <summary>
    /// Verifies that calling AddIntegratoR twice on the same service collection does not break
    /// the Durable Functions data converter wiring. Two Configure&lt;DurableTaskWorkerOptions&gt;
    /// actions stack and both fire on resolution; the last one wins. Either way the resolved
    /// options must still carry a working JsonDataConverter that round-trips Result&lt;T&gt;.
    /// </summary>
    [Fact]
    public void AddIntegratoR_CalledTwice_DurableTaskDataConverterStillRoundTripsResult()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        IConfiguration configuration = CreateMinimalConfiguration();

        // Act
        services.AddIntegratoR(configuration);
        services.AddIntegratoR(configuration);

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();
        DurableTaskWorkerOptions options = provider.GetRequiredService<IOptions<DurableTaskWorkerOptions>>().Value;
        options.DataConverter.Should().BeOfType<JsonDataConverter>();

        var error = new IntegrationError("Activity.Failed", "fail", ErrorType.Failure);
        Result<string> original = Result.Fail<string>(error);

        string? serialised = options.DataConverter.Serialize(original);
        Result<string>? roundTripped = options.DataConverter.Deserialize(serialised, typeof(Result<string>)) as Result<string>;

        roundTripped.Should().NotBeNull();
        roundTripped!.IsFailed.Should().BeTrue();
        ((IntegrationError)roundTripped.Errors[0]).Code.Should().Be("Activity.Failed");
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
        settings.Authentication.Mode.Should().Be(AuthenticationMode.OAuth);
        settings.Authentication.OAuth.ClientId.Should().Be("test-client-id");
    }
}

/// <summary>
/// Dummy model used to verify FluentValidation assembly scanning.
/// </summary>
public class DummyModel
{
    public string? Name { get; set; }
}

/// <summary>
/// Dummy validator discovered by <c>AddValidatorsFromAssembly</c> during tests.
/// </summary>
public class DummyModelValidator : AbstractValidator<DummyModel>
{
    public DummyModelValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}
