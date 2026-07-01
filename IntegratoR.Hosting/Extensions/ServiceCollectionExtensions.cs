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
    // Shared JsonSerializerOptions for the Durable Task data converter. STJ caches per-instance
    // converter metadata on the options, so a single static readonly instance keeps that cache
    // warm across the lifetime of the host. Matches the pattern in DistributedCacheService.
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

        // 3b. Cross-assembly generic handler closing.
        // MediatR v12 only closes open generics against types in the SAME scanned assembly,
        // so the layer-local AddMediatR calls in AddApplicationServices() and AddODataClientFOProxy()
        // never see the open CreateCommandHandler<T> and the entity types together. This single
        // combined scan emits the closed IRequestHandler<CreateCommand<T>, ...> registrations for
        // every F&O entity AND every consumer-supplied entity (a subclass of an F&O entity or a new
        // BaseEntity<TKey>), so mediator.Send(new CreateCommand<ConsumerEntity>(...)) resolves.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.RegisterServicesFromAssembly(
                typeof(IntegratoR.Application.Features.Common.Commands.CreateCommandHandler<>).Assembly);
            cfg.RegisterServicesFromAssembly(
                typeof(IntegratoR.OData.FO.Domain.Entities.LedgerJournal.LedgerJournalHeader).Assembly);

            // Fold consumer assemblies into the SAME RegisterGenericHandlers pass so the framework's
            // generic CRUD/query handlers are also closed over consumer entity types. Without this,
            // a consumer's extended/custom entity would have no IRequestHandler<CreateCommand<T>, ...>.
            foreach (Assembly assembly in builder.ConsumerAssemblies)
            {
                cfg.RegisterServicesFromAssembly(assembly);
            }
        });

        // 4. Durable Functions Result<T> support — register the System.Text.Json Result
        //    converters with the Durable Task worker so activities and orchestrators returning
        //    Result<T>/Result round-trip through the task hub. The Configure call is lazy:
        //    consumers not using Durable Functions never resolve DurableTaskWorkerOptions and
        //    pay zero runtime cost (the package reference itself is unconditional, but the
        //    Microsoft.DurableTask.* packages are tiny and almost always already in the
        //    dependency tree of an IntegratoR consumer building Azure Functions integrations).
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

        // 6. Register the F&O FluentValidation validators. The F&O layer (AddODataClientFOProxy)
        //    registers its MediatR handlers but not its validators; wiring them here keeps the
        //    FluentValidation.DependencyInjectionExtensions dependency in the composition root.
        //    This registers the NON-GENERIC, concrete validators (e.g. GetDimensionOrdersQueryValidator),
        //    which fire in the MediatR ValidationBehaviour.
        //
        //    AddValidatorsFromAssembly's scanner does NOT register OPEN-GENERIC validators (it cannot
        //    build a closed IValidator<> service type from a partially-open generic). The generic
        //    baseline validators (CreateCommandValidator<T> etc.) and the F&O-derived per-command
        //    validators are therefore closed and registered explicitly in step 6b below.
        services.AddValidatorsFromAssembly(
            typeof(IntegratoR.OData.FO.Domain.Entities.LedgerJournal.LedgerJournalHeader).Assembly,
            includeInternalTypes: true);

        // 6b. Close the OPEN-GENERIC command/query validators over every discovered entity type and
        //     register the resulting CLOSED IValidator<> so they actually fire in ValidationBehaviour.
        //     Step 6 (and AddApplicationServices) cannot: the FluentValidation scanner skips open
        //     generics, so mediator.Send(new CreateCommand<TEntity>(...)) would otherwise resolve an
        //     EMPTY IEnumerable<IValidator<CreateCommand<TEntity>>> and generic command validation
        //     would silently never run. Entities come from the F&O assembly AND consumer assemblies (a
        //     consumer's CreateCommand<ConsumerEntity> needs its validator too); the open-generic
        //     validators come from IntegratoR.Application (baseline) and IntegratoR.OData.FO (derived).
        //     (The F&O-derived per-command validators are also closed and registered here as a benign
        //     side-effect — nothing dispatches those FO-specific commands through the mediator today,
        //     and TryAddEnumerable keeps the registration harmless.) See open-todos #15.
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

        // 7. Register consumer FluentValidation validators. Consumer MediatR handlers — including
        //    the closed generic CRUD/query handlers for consumer entities — are already registered
        //    by the combined RegisterGenericHandlers scan in step 3b, so they must NOT be scanned
        //    again here (a second AddMediatR pass would emit duplicate handler registrations).
        foreach (Assembly assembly in builder.ConsumerAssemblies)
        {
            services.AddValidatorsFromAssembly(assembly);
        }

        return services;
    }

    // Open-generic validators (AbstractValidator<TRequest<TArg>>) are skipped by FluentValidation's
    // assembly scanner because there is no closed IValidator<> service type to bind. This closes each
    // over every discovered entity that satisfies its generic constraints and registers the resulting
    // closed IValidator<> so ValidationBehaviour resolves and runs them.
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

                // TryAddEnumerable dedupes on (service, implementation) so the pass is idempotent when
                // AddIntegratoR is called twice and never double-registers the same validator.
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

    // Whether 'candidate' satisfies every generic constraint declared on 'genericParameter', so
    // MakeGenericType(candidate) will not throw. Lets one pass cover baseline validators (constraint
    // IEntity) and F&O-derived validators (constraint LedgerJournalHeader/Line) without exceptions.
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
