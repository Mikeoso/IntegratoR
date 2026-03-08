# IntegrationError

Domain-specific error type that extends `FluentResults.Error` with a machine-readable code and an `ErrorType` for HTTP status mapping. Used throughout the framework to communicate failures via `Result`.

## Use the Error Type

```csharp
var error = new IntegrationError(
    "Journal.NotFound",
    "Journal JBN-001 not found in company USMF",
    ErrorType.NotFound);

Result<LedgerJournalHeader> result = Result.Fail<LedgerJournalHeader>(error);
// result.IsFailed == true
// result.GetError()?.Code == "Journal.NotFound"
```

## Constructor

```csharp
public IntegrationError(string code, string message, ErrorType type, Exception? exception = null)
```

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `code` | `string` | Yes | -- | Machine-readable error code (e.g. `"OData.Conflict"`) |
| `message` | `string` | Yes | -- | Human-readable error description |
| `type` | `ErrorType` | Yes | -- | Error category for HTTP status mapping |
| `exception` | `Exception?` | No | `null` | Underlying exception, attached via `CausedBy()` |

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Code` | `string` | Machine-readable error code |
| `Type` | `ErrorType` | Error category |
| `Exception` | `Exception?` | Underlying exception, if any |
| `Message` | `string` | Human-readable message (inherited from `FluentResults.Error`) |

## ErrorType Enum

```csharp
public enum ErrorType
{
    Failure,     // General failure       -> HTTP 500
    Validation,  // Input validation      -> HTTP 400
    NotFound,    // Entity not found      -> HTTP 404
    Conflict     // Concurrency conflict  -> HTTP 409
}
```

| Value | Typical HTTP Status | When to use |
|-------|-------------------|-------------|
| `Failure` | 500 Internal Server Error | Unexpected errors, external service failures |
| `Validation` | 400 Bad Request | Invalid input, business rule violations |
| `NotFound` | 404 Not Found | Entity lookup returned no results |
| `Conflict` | 409 Conflict | Optimistic concurrency violations, duplicate keys |

## ResultExtensions

Extension methods for ergonomic error access and pattern matching on `Result` types.

### GetError

Returns the first `IntegrationError` from the result's error list, or `null` if none exists.

```csharp
public static IntegrationError? GetError(this IResultBase result)
```

```csharp
Result result = Result.Fail(new IntegrationError("X.Error", "Something failed", ErrorType.Failure));

IntegrationError? error = result.GetError();
Console.WriteLine($"[{error?.Code}] {error?.Message}");
// Output: [X.Error] Something failed
```

### Match\<T, TOut\>

Pattern-matches on `Result<T>`, invoking the appropriate callback.

```csharp
public static TOut Match<T, TOut>(
    this Result<T> result,
    Func<T, TOut> onSuccess,
    Func<IntegrationError, TOut> onFailure)
```

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(query, cancellationToken);

string message = result.Match(
    header => $"Found journal: {header.JournalBatchNumber}",
    error => $"Error [{error.Code}]: {error.Message}"
);
// Success output: "Found journal: JBN-001"
// Failure output: "Error [OData.NotFound]: Entity not found"
```

### Match\<TOut\>

Pattern-matches on non-generic `Result`.

```csharp
public static TOut Match<TOut>(
    this Result result,
    Func<TOut> onSuccess,
    Func<IntegrationError, TOut> onFailure)
```

```csharp
Result result = await mediator.Send(new DeleteCommand<LedgerJournalHeader>(entity), cancellationToken);

int statusCode = result.Match(
    () => 204,
    error => error.Type switch
    {
        ErrorType.NotFound => 404,
        ErrorType.Validation => 400,
        ErrorType.Conflict => 409,
        _ => 500
    }
);
// Result: int — 204 on success, or 400/404/409/500 on failure
```

## See Examples

### Creating errors for each type

```csharp
// Validation error
var validationError = new IntegrationError(
    "Validation.Error", "'DataAreaId' must not be empty", ErrorType.Validation);

// Not found error
var notFoundError = new IntegrationError(
    "OData.NotFound", "Entity not found for key: JBN-999", ErrorType.NotFound);

// Conflict error
var conflictError = new IntegrationError(
    "OData.Conflict", "Entity was modified by another process", ErrorType.Conflict);

// Failure with exception
var failureError = new IntegrationError(
    "OData.RequestFailed", "HTTP 503 from D365", ErrorType.Failure,
    new HttpRequestException("Service Unavailable"));
```

### Mapping to HTTP responses in Azure Functions

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(command, cancellationToken);

return result.Match<LedgerJournalHeader, IActionResult>(
    header => new OkObjectResult(header),
    error => error.Type switch
    {
        ErrorType.Validation => new BadRequestObjectResult(error.Message),
        ErrorType.NotFound => new NotFoundObjectResult(error.Message),
        ErrorType.Conflict => new ConflictObjectResult(error.Message),
        _ => new StatusCodeResult(500)
    }
);
// Result: IActionResult — 200 OK with entity, or 400/404/409/500 with error message
```

### Accessing the wrapped exception

```csharp
if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    if (error?.Exception is HttpRequestException httpEx)
        Console.WriteLine($"HTTP error: {httpEx.Message}");
    // Output: HTTP error: Service Unavailable
}
```

## See Also

- [[API-ICommand]] — commands that produce IntegrationError on failure
- [[API-IQuery]] — queries that produce IntegrationError on failure
- [[API-Pipeline-Behaviours]] — validation behaviour that returns IntegrationError
