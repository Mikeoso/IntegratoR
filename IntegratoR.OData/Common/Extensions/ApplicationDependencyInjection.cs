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
        // Bind the "ODataSettings" section from appsettings.json (or other sources)
        // to the ODataSettings class, making it available via IOptions<ODataSettings>.
        services.Configure<ODataSettings>(configuration.GetSection("ODataSettings"));

        // Register the custom authentication handler. It's transient because HttpMessageHandler lifetimes
        // are managed by the HttpClientFactory.
        services.AddTransient<ODataAuthenticationHandler>();

        // Configure a named HttpClient specifically for our OData client.
        // Using IHttpClientFactory is a best practice for managing HttpClient instances.
        // Crucially, we add our authentication handler to its pipeline.
        services.AddHttpClient("ODataClient")
            .AddHttpMessageHandler<ODataAuthenticationHandler>();

        // Register the Simple.OData.Client as a singleton. A single, thread-safe instance
        // is reused throughout the application's lifetime for performance.
        services.AddSingleton<IODataClient>(serviceProvider =>
        {
            // Resolve the strongly-typed settings.
            var settings = serviceProvider.GetRequiredService<IOptions<ODataSettings>>().Value;

            // Create an HttpClient instance from the factory. This instance will have the
            // ODataAuthenticationHandler already configured in its pipeline.
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

        // Finally, register our generic ODataService as the concrete implementation for the
        // IService and other data access abstractions. When a handler requests IService<Customer, string>,
        // the DI container will provide an instance of ODataService<Customer, string>.
        services.AddScoped(typeof(IService<,>), typeof(ODataService<,>));
        services.AddScoped(typeof(IODataService<,>), typeof(ODataService<,>));
        services.AddScoped(typeof(IODataBatchService<,>), typeof(ODataService<,>));

        return services;
    }
}