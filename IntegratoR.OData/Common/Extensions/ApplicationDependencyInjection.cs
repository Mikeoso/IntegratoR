using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.Common.Authentication;
using IntegratoR.OData.Common.Services;
using IntegratoR.OData.Domain.Settings;
using IntegratoR.OData.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Simple.OData.Client;

namespace IntegratoR.OData.Common.Extensions;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file defines the Composition Root for the OData Infrastructure layer. It encapsulates all
// the complex configuration required to set up a resilient and properly authenticated OData client
// for communicating with Dynamics 365 F&O.
// </remarks>
// ---------------------------------------------------------------------------------------------

/// <summary>
/// Provides an extension method to configure and register all services related to the
/// OData infrastructure layer in the dependency injection (DI) container.
/// </summary>
public static class ApplicationDependencyInjection
{
    /// <summary>
    /// Configures and registers the OData client infrastructure using settings from the application's configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The application's configuration, used to bind OData settings from the "ODataSettings" section.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddODataClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ODataSettings>(configuration.GetSection("ODataSettings"));
        services.AddODataDependencies();
        return services;
    }

    /// <summary>
    /// Configures and registers the OData client infrastructure using a delegate for programmatic configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configureOptions">An action to configure the <see cref="ODataSettings"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddODataClient(this IServiceCollection services, Action<ODataSettings> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddODataDependencies();
        return services;
    }

    /// <summary>
    /// Encapsulates the registration of all OData-related services.
    /// </summary>
    private static void AddODataDependencies(this IServiceCollection services)
    {
        // Register the custom authentication handler
        services.AddTransient<ODataAuthenticationHandler>();

        // Configure a named HttpClient specifically for our OData client.
        services.AddHttpClient("ODataClient")
            .AddHttpMessageHandler<ODataAuthenticationHandler>();

        // Register the Simple.OData.Client
        services.AddSingleton<IODataClient>(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<ODataSettings>>().Value;

            var oDataHttpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("ODataClient");
            oDataHttpClient.Timeout = TimeSpan.FromSeconds(settings.Timeout);

            // Configure and create the ODataClient.
            var odataClientSettings = new ODataClientSettings(oDataHttpClient)
            {
                BaseUri = new Uri(settings.Url),
                RequestTimeout = TimeSpan.FromSeconds(settings.Timeout),
            };
            return new ODataClient(odataClientSettings);
        });

        // Register Services
        services.AddScoped(typeof(IService<>), typeof(ODataService<>));
        services.AddScoped(typeof(IODataService<>), typeof(ODataService<>));
        services.AddScoped(typeof(IODataBatchService<>), typeof(ODataService<>));
    }
}