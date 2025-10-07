using IntegratoR.OData.FO.Domain.Models.Settings;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegratoR.OData.FO.Common.Extensions
{
    public static class ApplicationDependencyInjection
    {
        public static IServiceCollection AddODataFOProxy(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<FOSettings>(configuration.GetSection("FOSettings"));

            //Register all MediatR handlers (for commands and queries) from the current assembly.
            services.AddMediatR(cfg =>
            {
                cfg.RegisterGenericHandlers = true;
                cfg.RegisterServicesFromAssembly(typeof(CreateLedgerJournalHeaderCommand<>).Assembly);
            });

            return services;
        }
    }
}
