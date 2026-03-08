# AddApplicationServices

Extension method on `IServiceCollection` that registers the entire Application layer: MediatR pipeline behaviours, command/query handlers, FluentValidation validators, caching, and authentication.

## Use the Extension Method

```csharp
using IntegratoR.Application.Common.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplicationServices();
```

## Method Signature

```csharp
public static IServiceCollection AddApplicationServices(this IServiceCollection services)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `services` | `IServiceCollection` | The DI container to register services into |

**Returns:** `IServiceCollection` for method chaining.

## What It Registers

### Pipeline Behaviours (order matters)

| Order | Behaviour | Lifetime | Purpose |
|-------|-----------|----------|---------|
| 1 | `LoggingBehaviour<,>` | Transient | Structured logging and performance timing |
| 2 | `ValidationBehaviour<,>` | Transient | FluentValidation fail-fast |
| 3 | `CachingBehaviour<,>` | Transient | Transparent query caching |

### MediatR

| Registration | Details |
|-------------|---------|
| MediatR handlers | Assembly-scanned from `IntegratoR.Application` |
| `RegisterGenericHandlers` | `true` -- enables generic handler resolution for `CreateCommand<T>`, etc. |

### FluentValidation

| Registration | Details |
|-------------|---------|
| Validators | Assembly-scanned from `IntegratoR.Application` (includes internal types) |

### Core Services

| Service | Implementation | Lifetime | Purpose |
|---------|---------------|----------|---------|
| `ICacheService` | `InMemoryCacheService` | Singleton | In-memory caching via `IMemoryCache` |
| `IAuthenticator` | `OAuthAuthenticator` | Singleton | OAuth 2.0 client credentials flow |
| `IMemoryCache` | Framework default | Singleton | Underlying memory cache |

## See Examples

### Azure Functions host

```csharp
using IntegratoR.Application.Common.Extensions;
using IntegratoR.OData.FO.Common.Extensions;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationServices();
        services.AddODataClient(context.Configuration);
        services.AddODataClientFOProxy(context.Configuration);
    })
    .Build();

host.Run();
```

### Replacing the cache service

The default `InMemoryCacheService` is suitable for single-instance applications. For scaled-out Azure Functions, replace it with a distributed implementation after calling `AddApplicationServices()`:

```csharp
services.AddApplicationServices();

// Override with distributed cache (e.g. Redis)
services.AddSingleton<ICacheService, RedisCacheService>();
```

### Adding additional validators

Validators from other assemblies (e.g. the OData.FO layer) are registered by their own DI extensions. They are automatically discovered by the `ValidationBehaviour`:

```csharp
services.AddApplicationServices();                    // Registers Application validators
services.AddODataClientFOProxy(configuration);        // Registers F&O validators
// ValidationBehaviour will run validators from both assemblies
```

### Verifying registration

```csharp
var provider = services.BuildServiceProvider();

// Verify MediatR is configured
var mediator = provider.GetRequiredService<IMediator>();

// Verify cache service
var cache = provider.GetRequiredService<ICacheService>();

// Verify authenticator
var auth = provider.GetRequiredService<IAuthenticator>();
```

## Keep in Mind

- This method must be called before layer-specific registrations (OData, OData.FO, RELion) because those layers depend on MediatR and the pipeline being configured.
- The pipeline behaviour order (Logging -> Validation -> Caching) is intentional: logging wraps everything, validation rejects invalid requests before they reach the cache, and caching runs just before the handler.
- `RegisterGenericHandlers = true` is required for the generic CQRS handlers (`CreateCommandHandler<T>`, etc.) to resolve correctly.

## See Also

- [[API-Pipeline-Behaviours]] — behaviours registered by this method
- [[API-ICacheableQuery]] — caching contract enabled by the cache behaviour
- [[API-IService]] — service interface that handlers depend on
