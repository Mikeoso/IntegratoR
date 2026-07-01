# Set Up Azure Functions Host

> Last verified against v2.0.1

The production `Program.cs` for an isolated-worker Functions host wires four things on top of the minimal setup in [Getting Started](Getting-Started): Azure Key Vault for secrets, Application Insights for telemetry, string-valued enums in HTTP bodies, and the Newtonsoft `Result<T>` converters for HTTP-trigger payloads. `AddIntegratoR` is the only IntegratoR call — it registers the whole framework.

> [!CAUTION]
> Only the **isolated worker** model is supported. The in-process Functions model is not — its host owns the DI container and JSON pipeline, so `AddIntegratoR`'s converter wiring and options never reach the runtime.

This is the host shipped in `IntegratoR.SampleFunction`:

```csharp
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Identity;
using IntegratoR.Abstractions.Common.Results;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

// Newtonsoft Result<T> converters — process-wide, set once before any JsonConvert call.
JsonConvert.DefaultSettings = () => new JsonSerializerSettings
{
    Converters = { new ResultJsonConverter(), new ResultGenericJsonConverter() }
};

ArgumentNullException keyVaultUriNotSetException = new("KeyVault URI is not set in environment variables.");

var host = new HostBuilder()
    .ConfigureAppConfiguration((context, config) =>
    {
        var environment = context.HostingEnvironment;

        config.SetBasePath(environment.ContentRootPath)
            .AddJsonFile($"{environment.EnvironmentName}.settings.json", optional: true, reloadOnChange: true);

        if (environment.IsDevelopment())
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

            var keyVaultUri = new Uri(keyVaultEnvironmentValue);
            config.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
        }
    })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var clientAssembly = Assembly.GetExecutingAssembly();

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Accept string-valued enums in HTTP request/response bodies.
        services.Configure<JsonSerializerOptions>(options =>
        {
            options.Converters.Add(new JsonStringEnumConverter());
        });

        services.AddIntegratoR(context.Configuration, integrator =>
        {
            integrator.AddConsumerHandlers(clientAssembly);
        });
    })
    .Build();

host.Run();
```

## The dual-serialiser reality

`Result<T>` rides two JSON serialisers, and each needs its own converters. One is wired for you; one is not.

| Serialiser | Used by | Converters | How they get wired |
|---|---|---|---|
| **System.Text.Json** | Durable Functions data converter, `DistributedCacheService` | `IntegratoR.Abstractions.Common.Results.SystemText.*` | **Auto** — `AddIntegratoR` calls `.AddResultConverters()` and sets `DurableTaskWorkerOptions.DataConverter`; `DistributedCacheService` registers them in its own options. You do nothing. |
| **Newtonsoft.Json** | HTTP-trigger bodies (`JsonConvert.Serialize/DeserializeObject`), journal file parsing | `IntegratoR.Abstractions.Common.Results.ResultJsonConverter` + `ResultGenericJsonConverter` | **Manual** — the `JsonConvert.DefaultSettings` block above. |

> [!NOTE]
> The Newtonsoft hook is process-global mutable state. Set `JsonConvert.DefaultSettings` once, at the very top of `Program.cs`, before any code path calls `JsonConvert`. Skip it and a failed `Result<T>` serialises to an empty `{}` over HTTP — the error `Code` and `Message` vanish.

Both converter families delegate to a shared shape helper so the wire format stays in lockstep. Do not wire only one side. See [Understand the Architecture](Understand-the-Architecture) for the full contract.

## Configuration sources

The `ConfigureAppConfiguration` block layers sources in order — each overrides the last.

| Source | Environment | Purpose |
|---|---|---|
| `<EnvironmentName>.settings.json` | any (optional) | Non-secret overrides — URLs, timeouts, feature flags. |
| `local.settings.json` | Development only | Development secrets. Gitignored, never deployed. |
| Environment variables | any | Azure App Settings surface here. |
| Azure Key Vault | non-Development | Production secrets via `DefaultAzureCredential`. |

Key Vault secrets map to configuration keys by name: a secret named `ODataSettings--Authentication--OAuth--ClientSecret` binds to `ODataSettings:Authentication:OAuth:ClientSecret`. `DefaultAzureCredential` resolves through Managed Identity in production and the developer's identity locally, so one `Program.cs` covers every environment. Grant the function app's Managed Identity **GET** on Key Vault secrets.

> [!WARNING]
> `local.settings.json` holds real secrets and must never ship. Set `CopyToPublishDirectory=Never` and rely on Key Vault in Azure. See [Authentication Modes](Authentication-Modes) for where each credential comes from.

## Application Insights

Two calls route worker telemetry to Application Insights:

- `AddApplicationInsightsTelemetryWorkerService` registers `TelemetryClient` and the standard initialisers.
- `ConfigureFunctionsApplicationInsights` routes worker `ILogger` output and dependency tracking through it.

The connection string comes from the `APPLICATIONINSIGHTS_CONNECTION_STRING` App Setting — no code change picks it up.

## String-valued enums in HTTP bodies

The worker's System.Text.Json accepts numeric enums only. HTTP triggers that read or write an enum — including the smoke-test triggers — need `JsonStringEnumConverter`:

```csharp
services.Configure<JsonSerializerOptions>(options =>
{
    options.Converters.Add(new JsonStringEnumConverter());
});
```

The worker's serialiser reads `IOptions<JsonSerializerOptions>`, so `Configure` mutates the live instance — no serialiser replacement. This also matches D365's own string-enum wire form (for example `NoYes` serialises as `"Yes"`, not `1`).

## Register the framework

`AddIntegratoR` composes every layer. `AddConsumerHandlers` scans the given assemblies for the consumer's commands, handlers, and validators, and closes the open-generic pipeline over them — pass every assembly that holds your entities or handlers.

```csharp
services.AddIntegratoR(context.Configuration, integrator =>
{
    integrator.AddConsumerHandlers(clientAssembly);

    integrator.ConfigureOData(settings =>
    {
        settings.Timeout = 180;
        settings.Resilience.RetryCount = 5;
    });

    integrator.ConfigureFO(fo =>
    {
        fo.DimensionFormatName = "Sachkontodimensionen";
    });
});
```

`ConfigureOData` and `ConfigureFO` are `PostConfigure` hooks — they run after JSON binding, so they override `ODataSettings`/`FOSettings` values from configuration. See [Configure OData](Configure-OData) for the settings tree.

`ODataSettingsValidator` runs at startup (`ValidateOnStart`), so an authentication header smuggled into `DefaultHeaders`, missing credentials for the selected `AuthenticationMode`, or an unrecognised mode value fails the host before it serves a request. A missing `Url` is caught separately at OData-client resolution — `NormaliseBaseUrl` returns a clear `ArgumentException` rather than a misleading `UriFormatException`.

## Project file

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>preview</LangVersion>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <OutputType>Exe</OutputType>
    <_FunctionsSkipCleanOutput>true</_FunctionsSkipCleanOutput>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Azure.Functions.Worker" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.ApplicationInsights" />
    <PackageReference Include="Azure.Identity" />
    <PackageReference Include="Azure.Extensions.AspNetCore.Configuration.Secrets" />
    <PackageReference Include="Newtonsoft.Json" />
    <PackageReference Include="IntegratoR.Hosting" />
  </ItemGroup>
</Project>
```

`IntegratoR.Hosting` pulls the rest of the framework transitively. Package versions live in `Directory.Packages.props` (Central Package Management), so declare no versions here.

## Verify after deployment

Confirm Key Vault resolution, OData connectivity, and the LINQ-to-OData translator end-to-end with a smoke-test trigger against the deployed app:

```bash
curl -s -X POST \
     https://your-function-app.azurewebsites.net/api/smoke/ledger-journal \
     -H "Content-Type: application/json" \
     -H "x-functions-key: <function-key>" \
     -d '{"Company":"USMF","JournalName":"GenJrn","AccountDisplayValue":"...","OffsetAccountDisplayValue":"...","Amount":100,"CurrencyCode":"USD"}'
```

The trigger runs a full `LedgerJournalHeader` CRUD cycle in the given company and returns a per-step response with a top-level `Success` flag. Pass account display values that exist in the target sandbox's chart of accounts. Each step reports its `Result<T>` outcome — on failure the step carries the `IntegrationError` `Code` and `Type` plus a generic `"Operation failed; see host logs for details."` message; the full server message is logged host-side only, never returned. A read-only field left in an update PATCH fails the step (HTTP 403 `ODataSecurityException`) as a `Result` rather than throwing. See [Run Smoke Tests](Run-Smoke-Tests) and [Handle Errors](Handle-Errors).

## See Also

- [Getting Started](Getting-Started) — the minimal host for local development
- [Configure OData](Configure-OData) — the `ODataSettings` tree
- [Authentication Modes](Authentication-Modes) — OAuth vs API Key and where each secret lives
- [Run Smoke Tests](Run-Smoke-Tests) — verify the deployment end-to-end
