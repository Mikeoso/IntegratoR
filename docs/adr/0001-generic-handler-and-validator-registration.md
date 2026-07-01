# ADR-0001: Cross-assembly generic handler & validator registration

- **Status:** Accepted
- **Date:** 2026-07-01

## Context

IntegratoR ships open-generic CQRS building blocks — `CreateCommandHandler<TEntity>`,
`GetByKeyQueryHandler<TEntity>`, `CreateCommandValidator<TEntity>`, the batch variants, and the
F&O-derived per-command validators — that must resolve for **every** entity type: the framework's own
F&O entities *and* a consumer's extended/custom entities.

Two library constraints make the naive registration fail:

1. **MediatR v12 closes open generics only against types in the *same* scanned assembly.** The
   layer-local `AddMediatR` calls (`AddApplicationServices`, `AddODataClientFOProxy`) never see the
   open `CreateCommandHandler<T>` and the entity types together, so `mediator.Send(new
   CreateCommand<LedgerJournalHeader>(…))` would have no registered `IRequestHandler`.
2. **FluentValidation's `AddValidatorsFromAssembly` scanner skips open-generic validators** — it
   cannot synthesise a closed `IValidator<>` service type from a partially-open generic. So the
   generic command validators were registered as *types* but never as a resolvable
   `IValidator<CreateCommand<TEntity>>`, and `ValidationBehaviour` resolved an **empty**
   `IEnumerable<IValidator<…>>` — generic command validation silently never ran (see open-todos #15).

## Decision

The composition root (`IntegratoR.Hosting/Extensions/ServiceCollectionExtensions.cs`) performs the
closing explicitly:

- **Handlers** — a single combined `AddMediatR(RegisterGenericHandlers = true)` scan that registers
  the Application assembly, the F&O assembly, **and** every consumer assembly *together*, so MediatR
  closes the open handlers over all entity types in one pass. Consumer assemblies are therefore NOT
  scanned again by a second `AddMediatR` (that would duplicate handler registrations).
- **Validators** — after `AddValidatorsFromAssembly` registers the non-generic validators,
  `RegisterClosedGenericValidators` reflects over the open-generic validators (from Application +
  F&O) and the entity types (from F&O + consumer assemblies), closes each validator over every entity
  that satisfies its generic constraints, and registers the closed `IValidator<>` via
  `TryAddEnumerable` (idempotent across repeated `AddIntegratoR` calls).

## Consequences

- Generic command/query validation now actually fires in `ValidationBehaviour`.
- **Do not "simplify" the combined step-3b scan back into per-layer `AddMediatR` calls**, and do not
  add a second consumer-assembly `AddMediatR` — either reintroduces the cross-assembly gap or
  duplicate registrations.
- `RegisterClosedGenericValidators` uses `GetLoadableTypes` (catching `ReflectionTypeLoadException`)
  because a consumer assembly may reference a type it cannot load; removing that guard makes
  `AddIntegratoR` crash on such consumers.
- The F&O-derived per-command validators are closed and registered as a benign side-effect; nothing
  dispatches those FO-specific commands through the mediator today, and `TryAddEnumerable` keeps it
  harmless.
- Revisit if MediatR gains cross-assembly open-generic closing or FluentValidation registers open
  generics natively.
