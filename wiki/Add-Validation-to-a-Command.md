# Add Validation to a Command

Create a FluentValidation validator for any command or query. The `ValidationBehaviour` pipeline automatically discovers and runs your validator before the request reaches its handler.

> **Prerequisites:** [[Install-the-Framework]]

## Create a Validator

```csharp
using FluentValidation;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

public class CreateLedgerJournalHeaderValidator
    : AbstractValidator<CreateCommand<LedgerJournalHeader>>
{
    public CreateLedgerJournalHeaderValidator()
    {
        RuleFor(x => x.Entity.DataAreaId)
            .NotEmpty()
            .WithMessage("'Data Area Id' must not be empty.");

        RuleFor(x => x.Entity.JournalName)
            .NotEmpty()
            .MaximumLength(10)
            .WithMessage("'Journal Name' must be between 1 and 10 characters.");

        RuleFor(x => x.Entity.Description)
            .NotEmpty()
            .MaximumLength(60)
            .WithMessage("'Description' must be between 1 and 60 characters.");
    }
}
```

No registration code is needed. The DI container auto-discovers all `AbstractValidator<T>` implementations when you call `services.AddApplicationServices()`.

## How the Pipeline Works

When you send a command through MediatR, the `ValidationBehaviour` intercepts it:

```
Request -> LoggingBehaviour -> ValidationBehaviour -> CachingBehaviour -> Handler
                                    |
                            Validators found?
                            /              \
                          No                Yes
                          |                  |
                     Pass through     Run all validators
                                          |
                                    Failures found?
                                    /              \
                                  No                Yes
                                  |                  |
                            Pass through     Short-circuit with
                                             Result.Fail(IntegrationError)
```

The behaviour runs all registered validators for the request type and collects failures. If any failures exist, the pipeline is short-circuited -- the handler is never invoked.

## Trigger a Validation Failure

```csharp
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;

var invalidJournal = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalName = "",        // empty -- violates NotEmpty rule
    Description = "Test"
};

Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(invalidJournal),
    cancellationToken);
```

Output:

```
result.IsFailed  = true
result.GetError().Code     = "Validation.Error"
result.GetError().Message  = "'Journal Name' must be between 1 and 10 characters."
result.GetError().Type     = ErrorType.Validation
```

The `ValidationBehaviour` returns the first validation failure as an `IntegrationError` with `ErrorType.Validation`. This standardises validation errors across the application.

## Validate Update and Delete Commands

The same pattern applies to `UpdateCommand<T>` and `DeleteCommand<T>`:

```csharp
public class UpdateLedgerJournalHeaderValidator
    : AbstractValidator<UpdateCommand<LedgerJournalHeader>>
{
    public UpdateLedgerJournalHeaderValidator()
    {
        RuleFor(x => x.Entity.JournalBatchNumber)
            .NotEmpty()
            .WithMessage("'Journal Batch Number' is required for updates.");

        RuleFor(x => x.Entity.Description)
            .NotEmpty()
            .MaximumLength(60);
    }
}
```

## When Things Go Wrong

**Validator not discovered** -- if your validator class is in an assembly that is not scanned during DI setup, it will not run. Ensure the assembly containing your validators is registered with `services.AddApplicationServices()` or that validators are in the same assembly as the commands.

**Multiple validators** -- if multiple validators exist for the same request type, all are executed. Only the first failure message is returned in the `IntegrationError`. All failures are collected internally, but the behaviour returns only the first to simplify client error handling.

## See Also

- [[Handle-Errors-with-Result]] — inspect validation failures returned in the Result
- [[Create-an-Entity]] — generic create command that validators run against
- [[Update-an-Entity]] — generic update command that validators run against
