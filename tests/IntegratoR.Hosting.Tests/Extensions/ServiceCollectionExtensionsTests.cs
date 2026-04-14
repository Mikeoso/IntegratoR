using System.Linq.Expressions;
using FluentAssertions;
using FluentResults;
using FluentValidation;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Authentication;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.Domain.Settings;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.FO.Domain.Models.Settings;
using IntegratoR.TestKit.Assertions;
using MediatR;
using Microsoft.DurableTask.Converters;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
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

    // MediatR v12 only closes open generics against entity types in the SAME scanned assembly.
    // The layer-local AddMediatR calls in AddApplicationServices() and AddODataClientFOProxy()
    // therefore never see the open CreateCommandHandler<T> and LedgerJournalHeader together.
    // AddIntegratoR has a combined-assembly scan (step 3b) that fixes this; these tests pin
    // the behaviour — if the scan is removed they fail with "No service for type
    // 'MediatR.IRequestHandler<...>'".
    private static (ServiceProvider Provider, IService<LedgerJournalHeader> Service) BuildProviderWithSubstitutedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        IConfiguration configuration = CreateMinimalConfiguration();

        services.AddIntegratoR(configuration);

        // Override the open-generic ODataService<> registration with a closed substitute so
        // the generic handlers resolve a fake instead of trying to hit the OData endpoint.
        IService<LedgerJournalHeader> substitute = Substitute.For<IService<LedgerJournalHeader>>();
        services.AddScoped(_ => substitute);

        return (services.BuildServiceProvider(), substitute);
    }

    private static LedgerJournalHeader CreateTestHeader() => new()
    {
        DataAreaId = "test",
        JournalName = "GenJrn",
        Description = "smoke-test"
    };

    [Fact]
    public async Task AddIntegratoR_ResolvesGenericCreateCommandHandler_ForFOEntity()
    {
        // Arrange
        (ServiceProvider provider, IService<LedgerJournalHeader> service) = BuildProviderWithSubstitutedService();
        await using (provider)
        {
            LedgerJournalHeader header = CreateTestHeader();
            service.AddAsync(header, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result.Ok(header)));

            using IServiceScope scope = provider.CreateScope();
            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            // Act
            Result<LedgerJournalHeader> result =
                await mediator.Send(new CreateCommand<LedgerJournalHeader>(header), TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeSuccessful();
            result.Value.Should().BeSameAs(header);
            await service.Received(1).AddAsync(header, Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task AddIntegratoR_ResolvesGenericUpdateCommandHandler_ForFOEntity()
    {
        // Arrange
        (ServiceProvider provider, IService<LedgerJournalHeader> service) = BuildProviderWithSubstitutedService();
        await using (provider)
        {
            LedgerJournalHeader header = CreateTestHeader();
            service.UpdateAsync(header, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result.Ok(header)));

            using IServiceScope scope = provider.CreateScope();
            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            // Act
            Result<LedgerJournalHeader> result =
                await mediator.Send(new UpdateCommand<LedgerJournalHeader>(header), TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeSuccessful();
            result.Value.Should().BeSameAs(header);
            await service.Received(1).UpdateAsync(header, Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task AddIntegratoR_ResolvesGenericDeleteCommandHandler_ForFOEntity()
    {
        // Arrange
        (ServiceProvider provider, IService<LedgerJournalHeader> service) = BuildProviderWithSubstitutedService();
        await using (provider)
        {
            LedgerJournalHeader header = CreateTestHeader();
            service.DeleteAsync(header, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result.Ok()));

            using IServiceScope scope = provider.CreateScope();
            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            // Act
            Result<LedgerJournalHeader> result =
                await mediator.Send(new DeleteCommand<LedgerJournalHeader>(header), TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeSuccessful();
            result.Value.Should().BeSameAs(header);
            await service.Received(1).DeleteAsync(header, Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task AddIntegratoR_ResolvesGenericGetByKeyQueryHandler_ForFOEntity()
    {
        // Arrange
        (ServiceProvider provider, IService<LedgerJournalHeader> service) = BuildProviderWithSubstitutedService();
        await using (provider)
        {
            LedgerJournalHeader header = CreateTestHeader();
            object[] key = ["test", "JB-001"];
            service.GetByKeyAsync(key, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result.Ok(header)));

            using IServiceScope scope = provider.CreateScope();
            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            // Act
            Result<LedgerJournalHeader> result =
                await mediator.Send(new GetByKeyQuery<LedgerJournalHeader>(key), TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeSuccessful();
            result.Value.Should().BeSameAs(header);
            await service.Received(1).GetByKeyAsync(key, Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task AddIntegratoR_ResolvesGenericGetByFilterQueryHandler_ForFOEntity()
    {
        // Arrange
        (ServiceProvider provider, IService<LedgerJournalHeader> service) = BuildProviderWithSubstitutedService();
        await using (provider)
        {
            LedgerJournalHeader header = CreateTestHeader();
            IEnumerable<LedgerJournalHeader> entities = [header];
            service.FindAsync(Arg.Any<Expression<Func<LedgerJournalHeader, bool>>?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result.Ok(entities)));

            using IServiceScope scope = provider.CreateScope();
            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            Expression<Func<LedgerJournalHeader, bool>> filter = h => h.DataAreaId == "test";

            // Act
            Result<IEnumerable<LedgerJournalHeader>> result =
                await mediator.Send(new GetByFilterQuery<LedgerJournalHeader>(filter), TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeSuccessful();
            result.Value.Should().ContainSingle().Which.Should().BeSameAs(header);
            await service.Received(1).FindAsync(Arg.Any<Expression<Func<LedgerJournalHeader, bool>>?>(), Arg.Any<CancellationToken>());
        }
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
