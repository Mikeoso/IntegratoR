# Add Validation

Validation runs as a MediatR pipeline behaviour. Any command or query with a registered FluentValidation `AbstractValidator<TRequest>` is validated **before** its handler runs — the handler can assume valid input.

## Define a Validator

```csharp
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

A validator is just a class deriving from `AbstractValidator<TRequest>` where `TRequest` is the MediatR request type. For generic commands the validator targets the closed generic — `CreateCommand<LedgerJournalHeader>`, not `CreateCommand<>`.

## Register Validators

The validator must live in an assembly registered via `AddConsumerHandlers(...)`:

```csharp
services.AddIntegratoR(context.Configuration, integrator =>
{
    integrator.AddConsumerHandlers(Assembly.GetExecutingAssembly());
});
```

`AddConsumerHandlers` scans the supplied assembly for MediatR handlers **and** FluentValidation validators in the same pass. Custom validators in third-party libraries can be added by passing their assembly explicitly:

```csharp
integrator.AddConsumerHandlers(
    Assembly.GetExecutingAssembly(),
    typeof(SomeExternalValidator).Assembly);
```

Internally `AddConsumerHandlers` registers each assembly via `services.AddValidatorsFromAssembly(...)` and `services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(...))`.

## How Validation Fits Into the Pipeline

The `ValidationBehaviour<TRequest, TResponse>` sits between `LoggingBehaviour` and `CachingBehaviour`. On each request:

1. Resolves all `IValidator<TRequest>` registrations.
2. Runs every validator against the request.
3. If **any** validator produces failures, the pipeline short-circuits — the handler never runs. The behaviour returns `Result.Fail(IntegrationError("Validation.Error", <first failure message>, ErrorType.Validation))`.
4. If validation passes (or no validators are registered), the request flows to the next behaviour.

> The behaviour returns only the **first** validation failure. Multiple `RuleFor` violations on the same request reach the consumer as a single error. This is intentional — most HTTP clients only surface one error at a time. Validators that need to surface multiple failures should compose the messages into a single rule (`When` / `Must` / custom validator).

## Validation Error Shape

The consumer sees:

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(command, cancellationToken);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    // error.Code    == "Validation.Error"
    // error.Type    == ErrorType.Validation
    // error.Message == "DataAreaId is required."   (first failure message)
}
```

See [Handle Errors](Handle-Errors) for how `ErrorType.Validation` maps to HTTP 400 Bad Request and the recommended HTTP response shape.

## Validate Entities, Not Just Commands

The example above validates a `CreateCommand<LedgerJournalHeader>`, but FluentValidation also supports child validators that apply to nested types. Define a reusable entity validator and refer to it from each command validator:

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

Both validators must be in an assembly registered via `AddConsumerHandlers(...)`.

## Validate Custom Queries

Queries are validated the same way:

```csharp
public sealed class GetByKeyQueryValidator<T>
    : AbstractValidator<GetByKeyQuery<T>>
    where T : class, IEntity
{
    public GetByKeyQueryValidator()
    {
        RuleFor(x => x.CompositeKey)
            .NotNull().WithMessage("Composite key is required.")
            .Must(k => k.Length > 0).WithMessage("Composite key must contain at least one value.");
    }
}
```

Open-generic validators close transparently as long as the closed generic of the request type matches.

## When Things Go Wrong

**Validator not running** — confirm the validator's assembly was passed to `AddConsumerHandlers`. The framework only scans assemblies listed explicitly.

**Multiple validators on the same request** — all run, all failures are collected, but only the first is surfaced to the consumer. Make rules pre-conditions of each other with `When(...)` if a specific failure should suppress others.

**Validator throws unexpectedly** — exceptions inside a validator propagate (they are not wrapped in `Result`). Validators should be pure rule definitions; if a validator needs async work, use `MustAsync` and ensure all DI dependencies are correctly registered.

## See Also

- [Send Commands](Send-Commands) — commands that flow through the validation pipeline
- [Run Queries](Run-Queries) — queries are validated identically
- [Handle Errors](Handle-Errors) — `ErrorType.Validation` and the `Validation.Error` code
- [Extend the Pipeline](Extend-the-Pipeline) — adding custom behaviours alongside `ValidationBehaviour`
- [Test with TestKit](Test-with-TestKit) — assert on validation results
