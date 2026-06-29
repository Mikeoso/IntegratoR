using System.Reflection;
using System.Text.Json;
using FluentValidation;
using IntegratoR.Abstractions.Common.Results.SystemText;
using IntegratoR.Application.Common.Extensions;
using IntegratoR.Hosting;
using IntegratoR.OData.Common.Extensions;
using IntegratoR.OData.Domain.Settings;
using IntegratoR.OData.FO.Common.Extensions;
using IntegratoR.OData.FO.Domain.Models.Settings;
using MediatR;
using Microsoft.DurableTask.Converters;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides a unified entry point for registering all IntegratoR framework services.
/// </summary>
public static class IntegratoRServiceCollectionExtensions
{
    // Shared JsonSerializerOptions for the Durable Task data converter. STJ caches per-instance
    // converter metadata on the options, so a single static readonly instance keeps that cache
    // warm across the lifetime of the host. Matches the pattern in DistributedCacheService.
    private static readonly JsonSerializerOptions DurableTaskJsonOptions =
        new JsonSerializerOptions(JsonSerializerDefaults.Web).AddResultConverters();

    /// <summary>
    /// Registers core IntegratoR framework services (Application, OData, F&amp;O) with default settings.
    /// Optional modules such as RELion must be registered separately.
    /// </summary>
    public static IServiceCollection AddIntegratoR(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddIntegratoR(configuration, _ => { });
    }

    /// <summary>
    /// Registers core IntegratoR framework services (Application, OData, F&amp;O) with builder-based configuration.
    /// Optional modules such as RELion must be registered separately.
    /// </summary>
    public static IServiceCollection AddIntegratoR(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IntegratoRBuilder> configure)
    {
        IntegratoRBuilder builder = new();
        configure(builder);

        // 1. Application layer — pipeline behaviours, MediatR, cache, auth
        services.AddApplicationServices();

        // 2. OData infrastructure — HTTP client, Polly, OData client
        services.AddODataClient(configuration);

        // 3. F&O layer — MediatR handlers for D365 entities
        services.AddODataClientFOProxy(configuration);

        // 3b. Cross-assembly generic handler closing.
        // MediatR v12 only closes open generics against types in the SAME scanned assembly,
        // so the layer-local AddMediatR calls in AddApplicationServices() and AddODataClientFOProxy()
        // never see the open CreateCommandHandler<T> and the entity types together. This single
        // combined scan emits the closed IRequestHandler<CreateCommand<T>, ...> registrations for
        // every F&O entity AND every consumer-supplied entity (a subclass of an F&O entity or a new
        // BaseEntity<TKey>), so mediator.Send(new CreateCommand<ConsumerEntity>(...)) resolves.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.RegisterServicesFromAssembly(
                typeof(IntegratoR.Application.Features.Common.Commands.CreateCommandHandler<>).Assembly);
            cfg.RegisterServicesFromAssembly(
                typeof(IntegratoR.OData.FO.Domain.Entities.LedgerJournal.LedgerJournalHeader).Assembly);

            // Fold consumer assemblies into the SAME RegisterGenericHandlers pass so the framework's
            // generic CRUD/query handlers are also closed over consumer entity types. Without this,
            // a consumer's extended/custom entity would have no IRequestHandler<CreateCommand<T>, ...>.
            foreach (Assembly assembly in builder.ConsumerAssemblies)
            {
                cfg.RegisterServicesFromAssembly(assembly);
            }
        });

        // 4. Durable Functions Result<T> support — register the System.Text.Json Result
        //    converters with the Durable Task worker so activities and orchestrators returning
        //    Result<T>/Result round-trip through the task hub. The Configure call is lazy:
        //    consumers not using Durable Functions never resolve DurableTaskWorkerOptions and
        //    pay zero runtime cost (the package reference itself is unconditional, but the
        //    Microsoft.DurableTask.* packages are tiny and almost always already in the
        //    dependency tree of an IntegratoR consumer building Azure Functions integrations).
        services.Configure<DurableTaskWorkerOptions>(options =>
        {
            options.DataConverter = new JsonDataConverter(DurableTaskJsonOptions);
        });

        // 5. Apply PostConfigure overrides if provided
        if (builder.ODataPostConfigure is not null)
        {
            services.PostConfigure(builder.ODataPostConfigure);
        }

        if (builder.FOPostConfigure is not null)
        {
            services.PostConfigure(builder.FOPostConfigure);
        }

        // 6. Register consumer FluentValidation validators. Consumer MediatR handlers — including
        //    the closed generic CRUD/query handlers for consumer entities — are already registered
        //    by the combined RegisterGenericHandlers scan in step 3b, so they must NOT be scanned
        //    again here (a second AddMediatR pass would emit duplicate handler registrations).
        foreach (Assembly assembly in builder.ConsumerAssemblies)
        {
            services.AddValidatorsFromAssembly(assembly);
        }

        return services;
    }
}
