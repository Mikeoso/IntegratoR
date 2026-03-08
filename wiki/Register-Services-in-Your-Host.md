# Register Services in Your Host

Wire up all IntegratoR services in your Azure Functions host. This page assumes you have [[Install-the-Framework|installed the packages]] and [[Configure-the-OData-Connection|configured ODataSettings]].

## Register All Services

```csharp
using IntegratoR.Application.Common.Extensions;
using IntegratoR.OData.Common.Extensions;
using IntegratoR.OData.FO.Common.Extensions;
using IntegratoR.RELion.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // 1. Application layer -- MediatR pipeline, validators, cache, auth
        services.AddApplicationServices();

        // 2. OData client -- HttpClient, Polly policies, ODataService<T>
        services.AddODataClient(configuration);

        // 3. D365 F&O proxy -- F&O-specific MediatR handlers, FOSettings
        services.AddODataClientFOProxy(configuration);

        // 4. RELion client -- HttpClient, auth handler, RELion handlers
        services.AddRelionClient(configuration);
    })
    .Build();

host.Run();
```

## Registration Methods

| Method | Layer | What It Registers |
|--------|-------|-------------------|
| `AddApplicationServices()` | Application | MediatR pipeline behaviours (Logging, Validation, Caching), validators, `InMemoryCacheService`, `OAuthAuthenticator` |
| `AddODataClient(configuration)` | OData | `ODataSettings` from config, `ODataAuthenticationHandler`, `ODataClient`, Polly retry + circuit breaker policies, `ODataService<T>` |
| `AddODataClientFOProxy(configuration)` | OData.FO | `FOSettings` from config, F&O-specific MediatR command/query handlers |
| `AddRelionClient(configuration)` | RELion | `RelionSettings` from config, `RelionAuthenticationHandler`, RELion HttpClient, `RelionService` |

## Pipeline Registration Order

`AddApplicationServices()` must be called **first**. It registers MediatR pipeline behaviours in this order:

```
Logging -> Validation -> Caching -> Handler
```

This means every request is logged, then validated (fail-fast on invalid input), then checked against the cache before reaching the actual handler. The registration order in the DI container determines execution order.

## Minimal Setup (OData Only)

If you do not need RELion, omit `AddRelionClient`:

```csharp
services.AddApplicationServices();
services.AddODataClient(configuration);
services.AddODataClientFOProxy(configuration);
```

## Programmatic Configuration

You can configure settings without `appsettings.json` using the action overload:

```csharp
services.AddODataClient(options =>
{
    options.Url = "https://your-environment.operations.dynamics.com/data";
    options.AuthMode = ODataAuthMode.OAuth;
    options.ClientId = "...";
    options.ClientSecret = "...";
    options.TenantId = "...";
    options.Resource = "https://your-environment.operations.dynamics.com";
});
```

## Common Mistakes

**Calling `AddODataClient` before `AddApplicationServices`** -- The MediatR pipeline behaviours will not wrap your handlers correctly. Always register the Application layer first.

## What Just Happened

- `AddApplicationServices()` set up the MediatR pipeline with logging, validation, and caching behaviours.
- `AddODataClient()` configured the HTTP client with authentication, retry policies, and circuit breaker.
- `AddODataClientFOProxy()` registered all F&O-specific command and query handlers with MediatR.
- Your host is now ready to send commands and queries through the CQRS pipeline.

## See Also

- [[Configure-the-OData-Connection]] — configure OData settings before registration
- [[Create-Your-First-Entity]] — define an entity to use with registered services
- [[Send-Your-First-Command]] — send a command through the registered pipeline
