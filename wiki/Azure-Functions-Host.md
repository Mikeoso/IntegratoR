# Azure Functions Host

```csharp
using System.Reflection;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

IHost host = new HostBuilder()
    .ConfigureAppConfiguration((context, config) =>
    {
        IHostEnvironment environment = context.HostingEnvironment;

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
            string keyVaultUri = Environment.GetEnvironmentVariable("ClientSecretKeyVaultURI")
                ?? throw new ArgumentNullException("KeyVault URI is not set in environment variables.");

            config.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
        }
    })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        Assembly clientAssembly = Assembly.GetExecutingAssembly();

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddIntegratoR(context.Configuration, integrator =>
        {
            integrator.AddConsumerHandlers(clientAssembly);
        });
    })
    .Build();

host.Run();
```

`AddIntegratoR` is the single composition root for the framework. It calls `AddApplicationServices`, `AddODataClient`, and `AddODataClientFOProxy` in the correct order, binds `ODataSettings` and `FOSettings`, and automatically wires the `Result<T>` converters onto `DurableTaskWorkerOptions.DataConverter` for consumers using Durable Functions. `AddConsumerHandlers` scans the provided assembly for host-level validators and MediatR handlers.

If your host also integrates RELion, call `services.AddRelionClient(context.Configuration)` after `AddIntegratoR` and register the Newtonsoft `Result<T>` converters via `JsonConvert.DefaultSettings`. See [[RELion]] for details.

## Configuration Hierarchy

Configuration sources load in order; later sources override earlier ones:

| Priority | Source | Environment |
|---|---|---|
| 1 | `{Environment}.settings.json` | All |
| 2 | `local.settings.json` | Development only |
| 3 | Environment variables | All |
| 4 | Azure Key Vault | Non-development only |

In development, `local.settings.json` provides secrets. In deployed environments, Azure Key Vault replaces it via `DefaultAzureCredential` (Managed Identity in Azure).

## Sample Function

The `IntegratoR.SampleFunction` project exposes a single HTTP trigger,
`LedgerJournalSmokeTestTrigger` (`POST /api/smoke/ledger-journal`), which exercises the
full CRUD path against a live D365 F&O sandbox — create header, get by key, filter by
`dataAreaId`, create balanced debit/credit lines, update, and best-effort cleanup. It uses
the generic `CreateCommand<T>`, `UpdateCommand<T>`, `DeleteCommand<T>`, `GetByKeyQuery<T>`,
and `GetByFilterQuery<T>` types from `IntegratoR.Abstractions` dispatched via MediatR, so
no feature-specific handler registration is required beyond `AddIntegratoR`.

## See Also

- [[Getting-Started]] — minimal setup for first-time users
- [[Configuration]] — full settings reference for OData and F&O
- [[Durable-Functions]] — orchestration patterns and `Result<T>` converter wiring
- [[Extending-the-Pipeline]] — `AddApplicationServices()` registration details
- [[D365-FO-Journals]] — journal commands used by the smoke test trigger
