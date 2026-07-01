using System.Reflection;
using FluentValidation;
using IntegratoR.Abstractions.Interfaces.Authentication;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.Application.Common.Authentication;
using IntegratoR.Application.Common.Behaviours;
using IntegratoR.Application.Common.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace IntegratoR.Application.Common.Extensions;

/// <summary>
/// Provides extension methods for configuring and registering the application layer's services
/// in the dependency injection (DI) container.
/// </summary>
internal static class ApplicationDependencyInjection
{
    /// <summary>
    /// Registers the application layer's services: the MediatR CQRS pipeline, cross-cutting behaviours, validators, and core services.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so that calls can be chained.</returns>
    /// <remarks>Pipeline behaviours are registered in execution order (Logging, Validation, Caching); see the architecture documentation for details.</remarks>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehaviour<,>));

        services.AddMediatR(cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
        });

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true);

        // InMemoryCacheService suits single-instance apps; replace with a distributed cache for scaled-out deployments.
        services.AddSingleton<ICacheService, InMemoryCacheService>();
        services.AddSingleton<IAuthenticator, OAuthAuthenticator>();

        services.AddMemoryCache();

        return services;
    }
}
