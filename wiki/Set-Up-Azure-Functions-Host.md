# Set Up Azure Functions Host

The minimal `Program.cs` shown in [Getting Started](Getting-Started) is enough for local development. Production deployments add Azure Key Vault for secret rotation, Application Insights for observability, and the Newtonsoft `Result<T>` converter for HTTP-trigger request and response bodies.

This page walks through the production-ready `Program.cs` shipped in `IntegratoR.SampleFunction`.

## Full Production Host

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

// 1. Wire the Newtonsoft Result<T> converters globally.
// JsonConvert.DefaultSettings is process-wide mutable state — set it ONCE at startup,
// before any code path that calls JsonConvert.SerializeObject / DeserializeObject.
JsonConvert.DefaultSettings = () => new JsonSerializerSettings
{
    Converters = { new ResultJsonConverter(), new ResultGenericJsonConverter() }
};

ArgumentNullException keyVaultUriNotSetException =
    new("KeyVault URI is not set in environment variables.");

var host = new HostBuilder()
    .ConfigureAppConfiguration((context, config) =>
    {
        var environment = context.HostingEnvironment;

        // 2. Load per-environment overrides (Development.settings.json, Production.settings.json, ...).
        config.SetBasePath(environment.ContentRootPath)
              .AddJsonFile($"{environment.EnvironmentName}.settings.json",
                           optional: true,
                           reloadOnChange: true);

        // 3. Local development: read local.settings.json (gitignored, never deployed).
        if (environment.IsDevelopment())
        {
            config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true);
        }

        config.AddEnvironmentVariables();

        // 4. Production: read secrets from Azure Key Vault via DefaultAzureCredential.
        //    Requires the function app's Managed Identity to have GET permission
        //    on Key Vault secrets.
        if (!environment.IsDevelopment())
        {
            var keyVaultEnvironmentValue =
                Environment.GetEnvironmentVariable("ClientSecretKeyVaultURI");

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

        // 5. Application Insights — telemetry from the worker.
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // 6. Configure the Functions Worker's STJ options so HTTP triggers
        //    accept string-valued enums in request/response bodies.
        //    The worker's JsonObjectSerializer reads from IOptions<JsonSerializerOptions>,
        //    so Configure<JsonSerializerOptions> mutates the live options instance.
        services.Configure<JsonSerializerOptions>(options =>
        {
            options.Converters.Add(new JsonStringEnumConverter());
        });

        // 7. The IntegratoR composition root.
        services.AddIntegratoR(context.Configuration, integrator =>
        {
            integrator.AddConsumerHandlers(clientAssembly);
        });
    })
    .Build();

host.Run();
```

## Step-by-Step

### Step 1 — Newtonsoft `Result<T>` Converters

`AddIntegratoR` auto-wires the System.Text.Json `Result<T>` converters for the OData layer and the Durable Functions data converter. **Newtonsoft.Json**, used by HTTP trigger request and response bodies (when consumers serialise via `JsonConvert.SerializeObject`) and by the RELion client, needs the converters wired manually via `JsonConvert.DefaultSettings`.

Wire this **before** the `HostBuilder` so any code path that fires during DI construction (some constructors deserialise pre-staged JSON) sees the converters. Setting `DefaultSettings` is process-wide and idempotent — a single statement at the top of `Program.cs` is enough.

### Step 2 — Per-environment Overrides

`<EnvironmentName>.settings.json` (e.g. `Production.settings.json`) sits at the function root and provides environment-specific overrides for non-secret values (URLs, timeouts, feature flags). The file is optional — production deployments typically rely on Azure App Settings instead.

### Step 3 — Local Settings

`local.settings.json` is the Azure Functions Core Tools convention. It is **always gitignored** and **never deployed** — it carries development-only secrets like an OAuth client secret for a development D365 environment.

### Step 4 — Azure Key Vault

The production path reads the `ClientSecretKeyVaultURI` environment variable (set by Azure App Settings or Bicep), and registers a Key Vault configuration provider. Every secret in the Key Vault becomes an accessible configuration value with the secret's name as the key — Key Vault secrets named `ODataSettings--Authentication--OAuth--ClientSecret` become available as the `ODataSettings:Authentication:OAuth:ClientSecret` configuration key.

`DefaultAzureCredential` cascades through Managed Identity → Azure CLI → Visual Studio → environment variables, so the same `Program.cs` works in production (Managed Identity), staging (Azure CLI), and local production-simulation (developer's identity).

### Step 5 — Application Insights

The two calls register the worker's telemetry to flow to Application Insights:

- `AddApplicationInsightsTelemetryWorkerService` registers `TelemetryClient` and the standard telemetry initialisers.
- `ConfigureFunctionsApplicationInsights` patches the Functions worker to route `ILogger` calls and dependency tracking through Application Insights.

The connection string comes from the `APPLICATIONINSIGHTS_CONNECTION_STRING` setting (the standard name). No code changes required to pick up the value.

### Step 6 — String-Valued Enums in HTTP Bodies

The Functions Worker uses System.Text.Json with default settings, which only accept numeric enum values. Smoke-test triggers (and any custom HTTP trigger that accepts an enum in its body) need the converter registered:

```csharp
services.Configure<JsonSerializerOptions>(options =>
{
    options.Converters.Add(new JsonStringEnumConverter());
});
```

This mutates the shared options instance — no `WorkerOptions.Serializer` replacement required. The change applies to every `HttpRequestData.ReadFromJsonAsync` and `HttpResponseData.WriteAsJsonAsync` call.

### Step 7 — IntegratoR

The framework composition. `AddConsumerHandlers(clientAssembly)` registers the consumer's MediatR handlers and FluentValidation validators. Pass additional assemblies if custom handlers live elsewhere.

Optional builder hooks:

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

If the integration also calls RELion, register the RELion client separately — see [Integrate with RELion](Integrate-with-RELion).

## Project File

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
    <PackageReference Include="IntegratoR.Hosting" />
  </ItemGroup>
</Project>
```

The single `IntegratoR.Hosting` package pulls in the rest of the framework transitively. Versions live in `Directory.Packages.props` (Central Package Management).

> `local.settings.json` must have `CopyToPublishDirectory=Never` — never deploy local secrets. For local `func start` runs against the publish output, copy the file manually into `bin/output/` as shown in [Run Smoke Tests](Run-Smoke-Tests).

## Deployment

Two artifacts deploy to Azure:

1. The published `bin/output/` folder (via `dotnet publish` or `azure-functions-deploy@v1` GitHub Action) becomes the function app's code.
2. Azure App Settings hold environment-specific values — `ClientSecretKeyVaultURI`, `APPLICATIONINSIGHTS_CONNECTION_STRING`, and any non-secret overrides.

Azure App Settings JSON nesting uses the **double-underscore** separator: `ODataSettings__Authentication__OAuth__ClientId` maps to `ODataSettings:Authentication:OAuth:ClientId` in `IConfiguration`.

## Verification After Deployment

Run the smoke-test trigger against the deployed function:

```bash
curl -s -X POST \
     https://your-function-app.azurewebsites.net/api/smoke/financial-dimensions \
     -H "Content-Type: application/json" \
     -H "x-functions-key: <function-key>" \
     -d '{"DimensionFormatName":"Sachkontodimensionen","HierarchyType":"DataEntityLedgerDimensionFormat"}'
```

A successful response confirms Key Vault secret resolution, OData connectivity, and the LINQ-to-OData translator end-to-end. See [Run Smoke Tests](Run-Smoke-Tests).

## See Also

- [Getting Started](Getting-Started) — the minimal `Program.cs` for local development
- [Configure OData](Configure-OData) — settings reference
- [Authentication Modes](Authentication-Modes) — OAuth vs APIM setup details
- [Run Smoke Tests](Run-Smoke-Tests) — verify the deployment
- [Release Notes and Versioning](Release-Notes-and-Versioning) — package version model
