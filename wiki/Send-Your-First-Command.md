# Send Your First Command

Send create, update, and delete commands through the CQRS pipeline. This page assumes you have [[Register-Services-in-Your-Host|registered services]] and [[Create-Your-First-Entity|defined an entity]].

## Create an Entity

```csharp
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Common.Results;
using MediatR;

// Build the entity
var header = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalName = "GenJrn",
    Description = "Monthly accruals - March 2026"
};

// Send the command
var command = new CreateCommand<LedgerJournalHeader>(header);
Result<LedgerJournalHeader> result = await mediator.Send(command, cancellationToken);
// Result: Result<LedgerJournalHeader> — Success with created entity, server-generated fields populated
```

## Check the Result

Every command returns a `Result<TEntity>` from FluentResults. Check success or failure:

```csharp
if (result.IsSuccess)
{
    LedgerJournalHeader created = result.Value;
    Console.WriteLine($"Created journal {created.JournalBatchNumber} in {created.DataAreaId}");
    // Output: Created journal JBN-000431 in USMF
}

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    Console.WriteLine($"[{error?.Code}] {error?.Message} (Type: {error?.Type})");
    // Output: [VALIDATION_ERROR] JournalName is required (Type: Validation)
}
```

## Pattern Matching with Match

Use `Match` for a more concise approach:

```csharp
string message = result.Match(
    onSuccess: entity => $"Created journal {entity.JournalBatchNumber}",
    onFailure: error => $"Failed: [{error.Code}] {error.Message}");

Console.WriteLine(message);
// Success output: Created journal JBN-000431
// Failure output: Failed: [ODATA_ERROR] Entity validation failed in D365
```

The `Match` method returns the first `IntegrationError` from the result. `IntegrationError` contains:

- **`Code`** -- Machine-readable error code (e.g. `VALIDATION_ERROR`, `ODATA_ERROR`)
- **`Message`** -- Human-readable description
- **`Type`** -- `ErrorType` enum: `Failure`, `Validation`, `NotFound`, `Conflict`

## Update an Entity

```csharp
var header = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalBatchNumber = "JBN-000431",
    JournalName = "GenJrn",
    Description = "Monthly accruals - March 2026 (updated)"
};

var command = new UpdateCommand<LedgerJournalHeader>(header);
Result<LedgerJournalHeader> result = await mediator.Send(command, cancellationToken);
// Result: Result<LedgerJournalHeader> — Success with updated entity
```

Properties marked `[ODataField(IgnoreOnUpdate = true)]` are automatically excluded from the PATCH payload.

## Delete an Entity

```csharp
var header = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalBatchNumber = "JBN-000431",
    JournalName = "GenJrn",
    Description = "Monthly accruals - March 2026"
};

var command = new DeleteCommand<LedgerJournalHeader>(header);
Result<LedgerJournalHeader> result = await mediator.Send(command, cancellationToken);
// Result: Result<LedgerJournalHeader> — Success if entity was deleted
```

## Handle Failures

A failed result never throws an exception. The pipeline catches errors and wraps them in `Result.Fail()`:

```csharp
// Validation failure (missing required fields)
var invalid = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalName = "",       // required field is empty
    Description = ""        // required field is empty
};

Result<LedgerJournalHeader> result = await mediator.Send(new CreateCommand<LedgerJournalHeader>(invalid), cancellationToken);
// result.IsFailed == true
// result.GetError()?.Type == ErrorType.Validation
```

## What Just Happened

- `CreateCommand<T>`, `UpdateCommand<T>`, and `DeleteCommand<T>` are generic record types that work with any entity implementing `IEntity`.
- The MediatR pipeline processes each command through Logging, Validation, and Caching behaviours before reaching the handler.
- The handler calls the OData service, which serialises the entity (respecting `[ODataField]` attributes) and sends the HTTP request to D365 F&O.
- The result is always a `Result<T>` -- never an exception for business logic errors.

## See Also

- [[Run-Your-First-Query]] — query entities after creating them
- [[Create-Your-First-Entity]] — define the entity class used in commands
- [[Register-Services-in-Your-Host]] — register MediatR and services first
