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
    /// Configures and registers the <see cref="IODataClient"/>, its underlying <see cref="HttpClient"/>
    /// pipeline with authentication, and the generic <see cref="ODataService{TEntity, TKey}"/> repository.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The application's configuration, used to bind OData settings.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <remarks>
    /// This method orchestrates the entire setup for the OData client by:
    /// 1. Binding the `ODataSettings` section from configuration to a strongly-typed options object.
    /// 2. Creating a named <c>HttpClient</c> and attaching the <see cref="ODataAuthenticationHandler"/>
    ///    to its message pipeline, ensuring every request is automatically authenticated.
    /// 3. Constructing a singleton instance of the <see cref="ODataClient"/>, feeding it the
    ///    pre-configured and authenticated <c>HttpClient</c>.
    /// 4. Registering the generic <see cref="ODataService{TEntity, TKey}"/> as the concrete
    ///    implementation for the application's <see cref="IService{TEntity, TKey}"/> abstraction.
    /// </remarks>
    public static IServiceCollection AddODataClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ODataSettings>(configuration.GetSection("ODataSettings"));

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
        services.AddScoped(typeof(IService<,>), typeof(ODataService<,>));
        services.AddScoped(typeof(IODataService<,>), typeof(ODataService<,>));
        services.AddScoped(typeof(IODataBatchService<,>), typeof(ODataService<,>));

        return services;
    }
}