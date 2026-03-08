# Set Up an Azure Functions Host

Configure a complete Azure Functions isolated worker host with IntegratoR services, configuration sources, and Durable Functions serialisation.

> **Prerequisites:** [[Install-the-Framework]]

## Create the Program.cs

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

// 1. Configure Newtonsoft.Json for Durable Functions state serialisation.
JsonConvert.DefaultSettings = () => new JsonSerializerSettings
{
    Converters = { new ResultJsonConverter(), new ResultGenericJsonConverter() }
};

var host = new HostBuilder()
    // 2. Set up configuration sources
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

        // Non-dev environments use Azure Key Vault for secrets
        if (!environment.IsDevelopment())
        {
            var keyVaultUri = Environment.GetEnvironmentVariable("ClientSecretKeyVaultURI")
                ?? throw new ArgumentNullException("KeyVault URI is not set in environment variables.");

            config.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
        }
    })
    // 3. Configure the Functions worker
    .ConfigureFunctionsWorkerDefaults()
    // 4. Register services in the correct order
    .ConfigureServices((context, services) =>
    {
        var clientAssembly = Assembly.GetExecutingAssembly();

        // Application Insights telemetry
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // IntegratoR layers — order matters for pipeline behaviours
        services.AddApplicationServices();           // Pipeline behaviours, cache, OAuth
        services.AddODataClient(context.Configuration);        // Generic OData client
        services.AddODataClientFOProxy(context.Configuration); // D365 F&O entities
        services.AddRelionClient(context.Configuration);       // RELion client

        // Host-level validators and MediatR handlers
        services.AddValidatorsFromAssembly(clientAssembly);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(clientAssembly));
    })
    .Build();

host.Run();
```

## Understand the Configuration Hierarchy

Configuration sources are loaded in order, with later sources overriding earlier ones:

| Priority | Source | Environment |
|---|---|---|
| 1 | `{Environment}.settings.json` | All |
| 2 | `local.settings.json` | Development only |
| 3 | Environment variables | All |
| 4 | Azure Key Vault | Non-development only |

In development, `local.settings.json` provides connection strings, OAuth credentials, and other secrets. In deployed environments, Azure Key Vault replaces it — secrets are fetched using `DefaultAzureCredential` (Managed Identity in Azure).

## Understand the DI Registration Order

The registration order is significant because the MediatR pipeline behaviours are registered in `AddApplicationServices()` and execute in registration order:

```text
Logging Behaviour -> Validation Behaviour -> Caching Behaviour -> Handler
```

Register `AddApplicationServices()` first so the behaviours wrap all subsequent handlers. Then register each layer's services:

```csharp
services.AddApplicationServices();              // 1. Pipeline behaviours
services.AddODataClient(context.Configuration);  // 2. OData infrastructure
services.AddODataClientFOProxy(context.Configuration); // 3. F&O entities + handlers
services.AddRelionClient(context.Configuration); // 4. RELion handlers
```

## Configure Durable Functions Serialisation

Durable Functions uses `Newtonsoft.Json` internally to serialise orchestration state. The `ResultJsonConverter` and `ResultGenericJsonConverter` ensure that `Result` and `Result<T>` objects survive round-trip serialisation:

```csharp
JsonConvert.DefaultSettings = () => new JsonSerializerSettings
{
    Converters = { new ResultJsonConverter(), new ResultGenericJsonConverter() }
};
```

This must be set **before** building the host, as Durable Functions reads `JsonConvert.DefaultSettings` at startup.

## When Things Go Wrong

**Missing Key Vault URI** — if `ClientSecretKeyVaultURI` is not set in non-development:

```text
System.ArgumentNullException: KeyVault URI is not set in environment variables.
```

Set the environment variable in your Azure Function App configuration.

**Wrong registration order** — if `AddApplicationServices()` is called after the layer registrations, pipeline behaviours (logging, validation, caching) will not wrap the handlers registered before it. Validation errors will pass through silently.

**Missing `local.settings.json` in development** — the file is configured as `optional: false` for development, so the host fails to start:

```text
System.IO.FileNotFoundException: The configuration file 'local.settings.json' was not found
and is not optional.
```

## See Also

- [[Register-Services-in-Your-Host]] — details on each `Add*` extension method
- [[Build-a-Durable-Functions-Orchestration]] — use orchestrations with IntegratoR
- [[Configure-the-OData-Connection]] — OData-specific settings
- [[Configure-the-RELion-Connection]] — RELion-specific settings
