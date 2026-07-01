# Add Validation
> Last verified against v2.0.1

Validation runs as a pipeline behaviour: `ValidationBehaviour` executes every registered FluentValidation validator for a command or query before its handler runs, so the handler assumes valid input. A registered validator on the request type is all you write.

Define a validator for a `CreateCommand<LedgerJournalHeader>` and register its assembly through `AddIntegratoR`.

```csharp
using System.Reflection;
using FluentValidation;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

public sealed class CreateLedgerJournalHeaderValidator
    : AbstractValidator<CreateCommand<LedgerJournalHeader>>
{
    public CreateLedgerJournalHeaderValidator()
    {
        RuleFor(x => x.Entity.DataAreaId)
            .NotEmpty().WithMessage("DataAreaId is required.")
            .Length(4).WithMessage("DataAreaId must be exactly 4 characters.");

        RuleFor(x => x.Entity.JournalName)
            .NotEmpty().WithMessage("JournalName is required.")
            .MaximumLength(10).WithMessage("JournalName must be 10 characters or fewer.");

        RuleFor(x => x.Entity.Description)
            .NotEmpty().WithMessage("Description is required.");
    }
}
```

`AbstractValidator<TRequest>` targets the closed request type — `CreateCommand<LedgerJournalHeader>`, never the open `CreateCommand<>`. Register the validator's assembly through the builder:

```csharp
services.AddIntegratoR(context.Configuration, integrator =>
{
    integrator.AddConsumerHandlers(Assembly.GetExecutingAssembly());
});
```

`AddConsumerHandlers` scans each assembly for MediatR handlers and FluentValidation validators in one pass. Pass a third-party assembly explicitly to pick up its validators:

```csharp
integrator.AddConsumerHandlers(
    Assembly.GetExecutingAssembly(),
    typeof(SomeExternalValidator).Assembly);
```

## Send a request and read the rejection

Send a `LedgerJournalHeader` that breaks a rule (`DataAreaId` "US" is two characters), and validation short-circuits before the OData call.

```csharp
LedgerJournalHeader header = new()
{
    DataAreaId = "US",              // fails Length(4)
    JournalName = "GENJ",
    Description = "April accruals",
};

Result<LedgerJournalHeader> result =
    await mediator.Send(new CreateCommand<LedgerJournalHeader>(header), cancellationToken);

if (result.IsFailed)
{
    IntegrationError error = result.GetError();
    // error.Code    == "Validation.Error"
    // error.Type    == ErrorType.Validation
    // error.Message == "DataAreaId must be exactly 4 characters."
}
```

`ValidationBehaviour` runs all validators, collects every failure, then returns the **first** as `IntegrationError("Validation.Error", <first message>, ErrorType.Validation)`. The handler never runs, so no request reaches D365. [Handle Errors](Handle-Errors) maps `ErrorType.Validation` to HTTP 400.

> [!NOTE]
> Only the first failure reaches the caller. Multiple broken `RuleFor` rules surface as a single error — most HTTP clients show one at a time. To report several at once, compose them into one rule with `Must` or a custom message.

## Where validation sits in the pipeline

The chain is `Logging → Validation → Caching → Handler`. `ValidationBehaviour` is second, so an invalid request never hits the cache or the handler. [Extend the Pipeline](Extend-the-Pipeline) covers the full order and adding behaviours.

## The framework validators that already fire

`AddIntegratoR` closes IntegratoR's open-generic command and query validators over **every** `IEntity` — the F&O entities and your custom ones — and registers each as a resolvable `IValidator<>`. These fire without any code from you (since v2.0.1):

| Validator | Guards |
|---|---|
| `CreateCommandValidator<T>` / `UpdateCommandValidator<T>` / `DeleteCommandValidator<T>` | `Entity` not null |
| `CreateBatchCommandValidator<T>` / `UpdateBatchCommandValidator<T>` / `DeleteBatchCommandValidator<T>` | batch list not null or empty |
| `GetByKeyQueryValidator<T>` | `CompositeKey` not null, non-empty, no null elements |
| `GetByFilterQueryValidator<T>` | `Filter` expression not null |

Send a `CreateCommand<LedgerJournalHeader>(null!)` and the built-in `CreateCommandValidator<LedgerJournalHeader>` rejects it before any handler runs:

```csharp
Result<LedgerJournalHeader> result =
    await mediator.Send(new CreateCommand<LedgerJournalHeader>(null!), cancellationToken);

// result.IsFailed
// result.GetError().Message == "Entity must not be null."
```

Your `CreateLedgerJournalHeaderValidator` and the framework's `CreateCommandValidator<LedgerJournalHeader>` both run against the same request; their failures aggregate and the first surfaces.

> [!CAUTION]
> Generic command validation fires only through `AddIntegratoR`, which closes the open-generic validators over each entity. FluentValidation's own assembly scanner skips open generics (see ADR-0001), so `AddApplicationServices` alone leaves `ValidationBehaviour` with an empty validator set and no generic validation runs.

## Reuse an entity validator across commands

Define one validator for the entity and reference it from each command validator with `SetValidator`:

```csharp
public sealed class LedgerJournalHeaderValidator : AbstractValidator<LedgerJournalHeader>
{
    public LedgerJournalHeaderValidator()
    {
        RuleFor(x => x.DataAreaId).NotEmpty().Length(4);
        RuleFor(x => x.JournalName).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Description).NotEmpty();
    }
}

public sealed class CreateLedgerJournalHeaderValidator
    : AbstractValidator<CreateCommand<LedgerJournalHeader>>
{
    public CreateLedgerJournalHeaderValidator(LedgerJournalHeaderValidator entityValidator)
    {
        RuleFor(x => x.Entity).SetValidator(entityValidator);
    }
}
```

Register both validators' assembly through `AddConsumerHandlers`.

## When things go wrong

**Custom validator never runs** — its assembly was not passed to `AddConsumerHandlers`. The framework scans only the assemblies you list plus its own; add yours to the builder call.

**Validator throws instead of returning a failure** — an exception inside a validator propagates and is not wrapped in a `Result`. Keep validators as pure rule definitions; for async lookups use `MustAsync` and register every dependency the validator injects.

**Composite-key query fails validation before the read** — `GetByKeyQueryValidator<T>` rejects a null or empty `CompositeKey` with `Validation.Error`. Build the key from the entity: `header.GetCompositeKey()` returns `[DataAreaId, JournalBatchNumber!]`.

## See Also

- [Send Commands](Send-Commands) — the commands that flow through validation
- [Handle Errors](Handle-Errors) — `ErrorType.Validation` and the `Validation.Error` code
- [Extend the Pipeline](Extend-the-Pipeline) — the behaviour chain around `ValidationBehaviour`
- [Test with TestKit](Test-with-TestKit) — assert on validation results
