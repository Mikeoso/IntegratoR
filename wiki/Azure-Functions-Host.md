# Azure Functions Host

```csharp
using System.Reflection;
using Azure.Identity;
using FluentValidation;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Application.Common.Extensions;
using IntegratoR.OData.Common.Extensions;
using IntegratoR.OData.FO.Common.Extensions;
using IntegratoR.RELion.Common.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

// Durable Functions state serialisation
JsonConvert.DefaultSettings = () => new JsonSerializerSettings
{
    Converters = { new ResultJsonConverter(), new ResultGenericJsonConverter() }
};

var host = new HostBuilder()
    .ConfigureAppConfiguration((context, config) =>
    {
        var environment = context.HostingEnvironment;

        config.SetBasePath(environment.ContentRootPath)
            .AddJsonFile($"{environment.EnvironmentName}.settings.json",
                optional: true, reloadOnChange: true);

        if (environment.IsDevelopment())
        {
            config.AddJsonFile("local.settings.json",
                optional: false, reloadOnChange: true);
        }

        config.AddEnvironmentVariables();

        if (!environment.IsDevelopment())
        {
            var keyVaultUri = Environment.GetEnvironmentVariable("ClientSecretKeyVaultURI")
                ?? throw new ArgumentNullException("KeyVault URI is not set in environment variables.");

            config.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
        }
    })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var clientAssembly = Assembly.GetExecutingAssembly();

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // IntegratoR layers — order matters for pipeline behaviours
        services.AddApplicationServices();                          // Pipeline behaviours, cache, OAuth
        services.AddODataClient(context.Configuration);             // Generic OData client
        services.AddODataClientFOProxy(context.Configuration);      // D365 F&O entities
        services.AddRelionClient(context.Configuration);            // RELion client

        // Host-level validators and MediatR handlers
        services.AddValidatorsFromAssembly(clientAssembly);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(clientAssembly));
    })
    .Build();

host.Run();
```

## Configuration Hierarchy

Configuration sources load in order; later sources override earlier ones:

| Priority | Source | Environment |
|---|---|---|
| 1 | `{Environment}.settings.json` | All |
| 2 | `local.settings.json` | Development only |
| 3 | Environment variables | All |
| 4 | Azure Key Vault | Non-development only |

In development, `local.settings.json` provides secrets. In deployed environments, Azure Key Vault replaces it via `DefaultAzureCredential` (Managed Identity in Azure).

## Durable Functions Serialisation

Durable Functions uses `Newtonsoft.Json` to serialise orchestration state. The `ResultJsonConverter` and `ResultGenericJsonConverter` ensure `Result` and `Result<T>` survive round-trip serialisation. Set `JsonConvert.DefaultSettings` **before** building the host.
