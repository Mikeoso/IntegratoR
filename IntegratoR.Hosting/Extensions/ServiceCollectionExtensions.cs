using System.Linq;
using System.Reflection;
using System.Text.Json;
using FluentValidation;
using IntegratoR.Abstractions.Common.Results.SystemText;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Application.Common.Extensions;
using IntegratoR.Hosting;
using IntegratoR.OData.Common.Extensions;
using IntegratoR.OData.Domain.Settings;
using IntegratoR.OData.FO.Common.Extensions;
using IntegratoR.OData.FO.Domain.Models.Settings;
using MediatR;
using Microsoft.DurableTask.Converters;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides a unified entry point for registering all IntegratoR framework services.
/// </summary>
public static class IntegratoRServiceCollectionExtensions
{
    // Single static instance keeps STJ's per-instance converter cache warm for the host lifetime; see ADR-0002.
    private static readonly JsonSerializerOptions DurableTaskJsonOptions =
        new JsonSerializerOptions(JsonSerializerDefaults.Web).AddResultConverters();

    /// <summary>
    /// Registers core IntegratoR framework services (Application, OData, F&amp;O) with default settings.
    /// </summary>
    /// <param name="services">The service collection to add the framework services to.</param>
    /// <param name="configuration">The configuration to bind the framework settings from.</param>
    /// <returns>The same service collection so that calls can be chained.</returns>
    public static IServiceCollection AddIntegratoR(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddIntegratoR(configuration, _ => { });
    }

    /// <summary>
    /// Registers core IntegratoR framework services (Application, OData, F&amp;O) with builder-based configuration.
    /// </summary>
    /// <param name="services">The service collection to add the framework services to.</param>
    /// <param name="configuration">The configuration to bind the framework settings from.</param>
    /// <param name="configure">A delegate that configures the <see cref="IntegratoRBuilder"/>.</param>
    /// <returns>The same service collection so that calls can be chained.</returns>
    public static IServiceCollection AddIntegratoR(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IntegratoRBuilder> configure)
    {
        IntegratoRBuilder builder = new();
        configure(builder);

        // 1. Application layer — pipeline behaviours, MediatR, cache, auth
        services.AddApplicationServices();

        // 2. OData infrastructure — HTTP client, Polly, OData client
        services.AddODataClient(configuration);

        // 3. F&O layer — MediatR handlers for D365 entities
        services.AddODataClientFOProxy(configuration);

        // 3b. Close the open-generic handlers over the framework AND consumer entity types in one
        //     combined scan (MediatR v12 only closes within a single scanned assembly). See ADR-0001.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.RegisterServicesFromAssembly(
                typeof(IntegratoR.Application.Features.Common.Commands.CreateCommandHandler<>).Assembly);
            cfg.RegisterServicesFromAssembly(
                typeof(IntegratoR.OData.FO.Domain.Entities.LedgerJournal.LedgerJournalHeader).Assembly);

            foreach (Assembly assembly in builder.ConsumerAssemblies)
            {
                cfg.RegisterServicesFromAssembly(assembly);
            }
        });

        // 4. Wire the Result<T> converters into the Durable Task worker (lazy — zero cost for
        //    consumers not using Durable Functions). See ADR-0002.
        services.Configure<DurableTaskWorkerOptions>(options =>
        {
            options.DataConverter = new JsonDataConverter(DurableTaskJsonOptions);
        });

        // 5. Apply PostConfigure overrides if provided
        if (builder.ODataPostConfigure is not null)
        {
            services.PostConfigure(builder.ODataPostConfigure);
        }

        if (builder.FOPostConfigure is not null)
        {
            services.PostConfigure(builder.FOPostConfigure);
        }

        // 6. Register the F&O non-generic validators (e.g. GetDimensionOrdersQueryValidator). The
        //    open-generic validators are closed separately in 6b. See ADR-0001.
        services.AddValidatorsFromAssembly(
            typeof(IntegratoR.OData.FO.Domain.Entities.LedgerJournal.LedgerJournalHeader).Assembly,
            includeInternalTypes: true);

        // 6b. Close the open-generic command/query validators over every entity so they fire in
        //     ValidationBehaviour (the FluentValidation scanner skips open generics). See ADR-0001, open-todos #15.
        RegisterClosedGenericValidators(
            services,
            validatorAssemblies:
            [
                typeof(IntegratoR.Application.Features.Common.Validators.CreateCommandValidator<>).Assembly,
                typeof(IntegratoR.OData.FO.Domain.Entities.LedgerJournal.LedgerJournalHeader).Assembly,
            ],
            entityAssemblies:
            [
                typeof(IntegratoR.OData.FO.Domain.Entities.LedgerJournal.LedgerJournalHeader).Assembly,
                .. builder.ConsumerAssemblies,
            ]);

        // 7. Register consumer validators. Consumer handlers are already closed in 3b — do NOT
        //    re-scan them into MediatR here, or handler registrations would be duplicated. See ADR-0001.
        foreach (Assembly assembly in builder.ConsumerAssemblies)
        {
            services.AddValidatorsFromAssembly(assembly);
        }

        return services;
    }

    // Closes each open-generic validator over every constraint-satisfying entity and registers the
    // resulting closed IValidator<> so ValidationBehaviour resolves them. See ADR-0001.
    private static void RegisterClosedGenericValidators(
        IServiceCollection services,
        Assembly[] validatorAssemblies,
        Assembly[] entityAssemblies)
    {
        List<Type> openValidators = validatorAssemblies
            .SelectMany(GetLoadableTypes)
            .Where(type => type is { IsAbstract: false, IsGenericTypeDefinition: true }
                           && type.GetGenericArguments().Length == 1
                           && GetValidatedRequestType(type) is not null)
            .Distinct()
            .ToList();

        List<Type> entityTypes = entityAssemblies
            .SelectMany(GetLoadableTypes)
            .Where(type => type is { IsAbstract: false, IsGenericTypeDefinition: false }
                           && typeof(IEntity).IsAssignableFrom(type))
            .Distinct()
            .ToList();

        foreach (Type openValidator in openValidators)
        {
            Type parameter = openValidator.GetGenericArguments()[0];

            foreach (Type entity in entityTypes)
            {
                if (!SatisfiesConstraints(parameter, entity))
                {
                    continue;
                }

                Type implementationType = openValidator.MakeGenericType(entity);
                Type requestType = GetValidatedRequestType(implementationType)!;    // e.g. CreateCommand<TEntity> (closed)
                Type serviceType = typeof(IValidator<>).MakeGenericType(requestType);

                // TryAddEnumerable keeps the pass idempotent across repeated AddIntegratoR calls.
                services.TryAddEnumerable(ServiceDescriptor.Transient(serviceType, implementationType));
            }
        }
    }

    // A consumer assembly may reference a type it cannot load; Assembly.GetTypes() would then throw
    // ReflectionTypeLoadException and crash AddIntegratoR. Fall back to the types that DID load.
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null)!;
        }
    }

    // Returns the TRequest of the nearest AbstractValidator<TRequest> in the inheritance chain, or
    // null when the type does not derive from AbstractValidator<>.
    private static Type? GetValidatedRequestType(Type validatorType)
    {
        for (Type? current = validatorType.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            {
                return current.GetGenericArguments()[0];
            }
        }

        return null;
    }

    // Whether MakeGenericType(candidate) will succeed for genericParameter's constraints — lets one
    // pass cover both the IEntity-constrained and F&O-entity-constrained validators without throwing.
    private static bool SatisfiesConstraints(Type genericParameter, Type candidate)
    {
        GenericParameterAttributes attributes =
            genericParameter.GenericParameterAttributes & GenericParameterAttributes.SpecialConstraintMask;

        if (attributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint) && candidate.IsValueType)
        {
            return false;
        }

        if (attributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint) && !candidate.IsValueType)
        {
            return false;
        }

        if (attributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint)
            && !candidate.IsValueType
            && candidate.GetConstructor(Type.EmptyTypes) is null)
        {
            return false;
        }

        foreach (Type constraint in genericParameter.GetGenericParameterConstraints())
        {
            // Skip constraints that are themselves still open (none in our validators today).
            if (constraint.ContainsGenericParameters)
            {
                continue;
            }

            if (!constraint.IsAssignableFrom(candidate))
            {
                return false;
            }
        }

        return true;
    }
}
