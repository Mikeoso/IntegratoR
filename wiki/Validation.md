# Validation

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

Validators in the IntegratoR library assemblies are registered by `services.AddApplicationServices()`. Validators in your own consumer assembly must be registered separately via `services.AddValidatorsFromAssembly(clientAssembly)` in your host's DI setup — they are not auto-discovered.

## Pipeline

The `ValidationBehaviour<TRequest, TResponse>` intercepts every MediatR request. It runs all registered validators for the request type, and if any fail, short-circuits the pipeline with `Result.Fail(IntegrationError)` — the handler is never invoked.

```
Request -> LoggingBehaviour -> ValidationBehaviour -> CachingBehaviour -> Handler
```

## Validation Failures

```csharp
var invalidJournal = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalName = "",        // empty — violates NotEmpty rule
    Description = "Test"
};

Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(invalidJournal),
    cancellationToken);
// result.IsFailed          == true
// result.GetError().Code    == "Validation.Error"
// result.GetError().Message == "'Journal Name' must not be empty."
// result.GetError().Type    == ErrorType.Validation
```

The first validation failure is returned as an [[Error-Handling|`IntegrationError`]] with `ErrorType.Validation`.

## Update and Delete Validators

The same pattern works for `UpdateCommand<T>` and `DeleteCommand<T>`:

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

If multiple validators exist for the same request type, all are executed but only the first failure is returned in the result.

## See Also

- [[Error-Handling]] — `IntegrationError` and `ErrorType` reference
- [[Extending-the-Pipeline]] — pipeline behaviour order and custom behaviours
- [[Commands]] — generic commands that validators target
- [[Testing]] — `HaveErrorCode()` assertions for validation tests
