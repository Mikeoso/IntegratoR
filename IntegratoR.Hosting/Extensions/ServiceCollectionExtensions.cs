using System.Reflection;
using FluentValidation;
using IntegratoR.Application.Common.Extensions;
using IntegratoR.Hosting;
using IntegratoR.OData.Common.Extensions;
using IntegratoR.OData.Domain.Settings;
using IntegratoR.OData.FO.Common.Extensions;
using IntegratoR.OData.FO.Domain.Models.Settings;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides a unified entry point for registering all IntegratoR framework services.
/// </summary>
public static class IntegratoRServiceCollectionExtensions
{
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

        // 4. Apply PostConfigure overrides if provided
        if (builder.ODataPostConfigure is not null)
        {
            services.PostConfigure(builder.ODataPostConfigure);
        }

        if (builder.FOPostConfigure is not null)
        {
            services.PostConfigure(builder.FOPostConfigure);
        }

        // 5. Register consumer assemblies for MediatR + FluentValidation
        foreach (Assembly assembly in builder.ConsumerAssemblies)
        {
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
            services.AddValidatorsFromAssembly(assembly);
        }

        return services;
    }
}
