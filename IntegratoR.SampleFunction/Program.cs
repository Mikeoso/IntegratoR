using Azure.Identity;
using FluentValidation;
using IntegratoR.Application.Common.Extensions;
using IntegratoR.OData.Common.Extensions;
using IntegratoR.OData.FO.Common.Extensions;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;
using IntegratoR.RELion.Common.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

ArgumentNullException keyVaultUriNotSetException = new("KeyVault URI is not set in environment variables.");

var host = new HostBuilder()
    .ConfigureAppConfiguration((context, config) =>
    {
        var environment = context.HostingEnvironment;

        config.SetBasePath(context.HostingEnvironment.ContentRootPath)
            .AddJsonFile($"{context.HostingEnvironment.EnvironmentName}.settings.json", optional: true, reloadOnChange: true);

        if ((environment.IsDevelopment()))
        {
            config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true);
        }
        config.AddEnvironmentVariables();

        if (!environment.IsDevelopment())
        {
            var keyVaultEnvironmentValue = Environment.GetEnvironmentVariable("ClientSecretKeyVaultURI");
            if (string.IsNullOrEmpty(keyVaultEnvironmentValue))
            {
                throw keyVaultUriNotSetException;
            }
            var keyVaultURI = new Uri(keyVaultEnvironmentValue);
            config.AddAzureKeyVault(keyVaultURI, new DefaultAzureCredential());
        }
    })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var clientAssembly = Assembly.GetExecutingAssembly();

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddApplicationServices();
        services.AddODataClient(context.Configuration);
        services.AddODataFOProxy(context.Configuration);
        services.AddRelionClient(context.Configuration);

        services.AddValidatorsFromAssembly(clientAssembly);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(clientAssembly));
    })
    .Build();

host.Run();
