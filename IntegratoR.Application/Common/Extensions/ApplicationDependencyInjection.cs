using IntegratoR.Abstractions.Interfaces.Authentication;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.Application.Common.Authentication;
using IntegratoR.Application.Common.Behaviours;
using IntegratoR.Application.Common.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace IntegratoR.Application.Common.Extensions;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file defines the Composition Root for the application layer. It follows the principles
// of Clean Architecture by encapsulating all of the layer's service registrations into a single,
// cohesive extension method. This keeps the main application startup (e.g., in an Azure Function's
// Startup.cs or a Program.cs) clean and decoupled from the internal details of this layer.
// </remarks>
// ---------------------------------------------------------------------------------------------

/// <summary>
/// Provides extension methods for configuring and registering the application layer's services
/// in the dependency injection (DI) container.
/// </summary>
public static class ApplicationDependencyInjection
{
    /// <summary>
    /// Scans the application assembly and registers all of its services, including the full
    /// CQRS pipeline with MediatR, cross-cutting behaviors, and other core application services.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <remarks>
    /// This method is the single entry point for setting up the application layer. It handles:
    /// 1. **MediatR Pipeline Behaviors:** Registers the cross-cutting concerns that wrap every request.
    /// 2. **MediatR Handlers:** Scans the assembly to find and register all command and query handlers.
    /// 3. **Core Services:** Registers essential application services like authentication and caching.
    /// </remarks>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register MediatR pipeline behaviors.
        // Note: The order of registration is critical as it defines the execution order of the pipeline.
        // The flow will be: Logging -> Validation -> Caching -> Handler
        // 1. LoggingBehaviour wraps everything to log the entire process.
        // 2. ValidationBehaviour runs next to "fail fast" on invalid requests before hitting the cache or handler.
        // 3. CachingBehaviour runs just before the handler to maximize performance by returning cached data if available.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehaviour<,>));

        // Register all MediatR handlers (for commands and queries) from the current assembly.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
        });

        // Register core application services.
        // These are registered as Singletons as they are designed to be thread-safe and maintain state (like a cache)
        // for the lifetime of the application.
        // Note: InMemoryCacheService is suitable for single-instance apps. For scaled-out apps (e.g., Azure Functions),
        // replace this with a distributed cache implementation.
        services.AddSingleton<ICacheService, InMemoryCacheService>();
        services.AddSingleton<IAuthenticator, OAuthAuthenticator>();

        // Register the underlying IMemoryCache dependency required by the services above.
        services.AddMemoryCache();

        return services;
    }
}