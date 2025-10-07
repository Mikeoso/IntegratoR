using IntegratoR.RELion.Common.Authentication;
using IntegratoR.RELion.Common.Services;
using IntegratoR.RELion.Domain.Settings;
using IntegratoR.RELion.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace IntegratoR.RELion.Common.Extensions;

/// <summary>
/// Initializes and configures services related to the Relion client.
/// </summary>
public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddRelionClient(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure relion settings by reading the RelionSettings section from configuration
        services.Configure<RelionSettings>(configuration.GetSection("RelionSettings"));
        services.AddTransient<RelionAuthenticationHandler>();

        // Register all MediatR handlers (for commands and queries) from the current assembly.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
        });

        // Configure custom http client factory for OData client
        services.AddHttpClient("RelionApiClient").AddHttpMessageHandler<RelionAuthenticationHandler>();
        services.AddScoped(typeof(IRelionService), typeof(RelionService));

        return services;
    }
}
