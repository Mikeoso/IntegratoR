# Handle Errors with Result

IntegratoR uses `FluentResults.Result<T>` for all return values. Business errors are returned as `IntegrationError` instances inside the result -- exceptions are reserved for truly exceptional situations.

> **Prerequisites:** [[Install-the-Framework]]

## Check Success or Failure

```csharp
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;

Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(journal),
    cancellationToken);
// Result: Result<LedgerJournalHeader> — Success with entity or Failure with IntegrationError

if (result.IsSuccess)
{
    LedgerJournalHeader created = result.Value;
    // Use the created entity
}

if (result.IsFailed)
{
    // Handle the error
}
```

## Read the IntegrationError

`IntegrationError` extends `FluentResults.Error` with structured error information:

```csharp
using IntegratoR.Abstractions.Common.Results;

IntegrationError? error = result.GetError();

string code      = error.Code;       // "OData.Error"
string message   = error.Message;    // "Resource not found for the segment 'LedgerJournalHeaders'."
ErrorType type   = error.Type;       // ErrorType.NotFound
Exception? ex    = error.Exception;  // Original exception, if any
```

`GetError()` is an extension method that returns the first `IntegrationError` from the result's error list, or `null` if no `IntegrationError` is present.

## ErrorType Enum

| ErrorType | Meaning | Typical HTTP Mapping |
|-----------|---------|---------------------|
| `Failure` | General failure (OData errors, unexpected issues) | 500 |
| `Validation` | Input validation failed | 400 |
| `NotFound` | Entity or resource not found | 404 |
| `Conflict` | Conflicting state (e.g. duplicate, already posted) | 409 |

## Pattern Match with Match()

Use `Match()` to handle success and failure in a single expression. This works for both `Result<T>` and non-generic `Result`.

**Generic Result<T>:**

```csharp
using IntegratoR.Abstractions.Common.Results;

string message = result.Match(
    onSuccess: journal => $"Created journal {journal.JournalBatchNumber}",
    onFailure: error => $"Failed: [{error.Code}] {error.Message}");
// Result: "Created journal JBN-000431" or "Failed: [OData.Error] ..."
```

**Non-generic Result** (returned by batch and delete operations):

```csharp
Result batchResult = await mediator.Send(
    new CreateBatchCommand<LedgerJournalHeader>(journals),
    cancellationToken);
// Result: Result — Success or Failure (no entity value returned)

string message = batchResult.Match(
    onSuccess: () => "Batch completed successfully",
    onFailure: error => $"Batch failed: [{error.Code}] {error.Message}");
// Result: "Batch completed successfully" or "Batch failed: [OData.Error] ..."
```

The `onFailure` callback receives the first `IntegrationError`. If no `IntegrationError` exists in the result, a fallback error with code `"Unknown"` and type `ErrorType.Failure` is provided.

## Map ErrorType to HTTP Status Codes

In an Azure Function, map the `ErrorType` to an appropriate HTTP response:

```csharp
using IntegratoR.Abstractions.Common.Results;
using Microsoft.AspNetCore.Mvc;

IActionResult response = result.Match<LedgerJournalHeader, IActionResult>(
    onSuccess: journal => new OkObjectResult(journal),
    onFailure: error => error.Type switch
    {
        ErrorType.Validation => new BadRequestObjectResult(error.Message),
        ErrorType.NotFound   => new NotFoundObjectResult(error.Message),
        ErrorType.Conflict   => new ConflictObjectResult(error.Message),
        _                    => new StatusCodeResult(500)
    });
// Result: IActionResult — 200 OK, 400, 404, 409, or 500 depending on outcome
```

## When Things Go Wrong

**Accessing Value on a failed result** -- `result.Value` throws if the result is failed:

```csharp
// DON'T do this:
LedgerJournalHeader journal = result.Value; // throws if IsFailed

// DO this instead:
if (result.IsSuccess)
{
    LedgerJournalHeader journal = result.Value;
}

// Or use Match():
result.Match(
    onSuccess: journal => /* safe access */,
    onFailure: error => /* handle error */);
```

**No IntegrationError present** -- `GetError()` returns `null` if the errors list contains only plain `FluentResults.Error` instances:

```csharp
IntegrationError? error = result.GetError();
if (error is null)
{
    // Fall back to the raw error message
    string message = result.Errors.FirstOrDefault()?.Message ?? "Unknown error";
}
```

## See Also

- [[Add-Validation-to-a-Command]] — validation failures returned as Result errors
- [[Create-an-Entity]] — example command that returns Result
- [[Configure-Retry-and-Circuit-Breaker]] — resilience policies that produce Result errors
