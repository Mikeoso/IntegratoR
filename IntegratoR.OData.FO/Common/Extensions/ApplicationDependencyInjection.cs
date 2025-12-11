using IntegratoR.OData.FO.Domain.Models.Settings;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegratoR.OData.FO.Common.Extensions
{
    /// <summary>
    /// Provides extension methods for <see cref="IServiceCollection"/> to configure and register
    /// the necessary services for the D365 Finance & Operations OData client proxy.
    /// </summary>
    public static class ApplicationDependencyInjection
    {
        /// <summary>
        /// Adds and configures the D365 F&O OData client services using application configuration.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
        /// <param name="configuration">The application's <see cref="IConfiguration"/> instance.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddODataClientFOProxy(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<FOSettings>(configuration.GetSection("FOSettings"));
            services.AddODataDependenciesFOProxy();
            return services;
        }

        /// <summary>
        /// Adds and configures the D365 F&O OData client services using a configuration delegate.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
        /// <param name="foSettings">An <see cref="Action{FOSettings}"/> to configure the F&O settings.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddODataClientFOProxy(this IServiceCollection services, Action<FOSettings> foSettings)
        {
            services.Configure(foSettings);
            services.AddODataDependenciesFOProxy();
            return services;
        }

        /// <summary>
        /// Registers the core dependencies for the OData client proxy, such as MediatR handlers.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        private static IServiceCollection AddODataDependenciesFOProxy(this IServiceCollection services)
        {
            // Register all MediatR handlers
            services.AddMediatR(cfg =>
            {
                cfg.RegisterGenericHandlers = true;
                cfg.RegisterServicesFromAssembly(typeof(CreateLedgerJournalHeaderCommand<>).Assembly);
            });

            return services;
        }
    }
}
