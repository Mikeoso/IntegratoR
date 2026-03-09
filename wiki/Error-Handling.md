# Error Handling

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(journal), cancellationToken);

string output = result.Match(
    onSuccess: header => $"Created journal {header.JournalBatchNumber}",
    onFailure: error => $"Failed: [{error.Code}] {error.Message}");
// "Created journal JBN-000431" or "Failed: [OData.Error] Resource not found..."
```

IntegratoR uses `FluentResults.Result<T>` for all return values. Business errors are returned as `IntegrationError` instances inside the result — exceptions are reserved for truly exceptional situations.

## Result\<T\> — Success or Failure

```csharp
if (result.IsSuccess)
{
    LedgerJournalHeader created = result.Value; // safe to access
}

if (result.IsFailed)
{
    IntegrationError? error = result.GetError(); // first IntegrationError, or null
}
```

`GetError()` (`public static IntegrationError? GetError(this IResultBase result)`) returns the first `IntegrationError` from the result's error list. Returns `null` if only plain `FluentResults.Error` instances are present.

## IntegrationError

`IntegrationError` extends `FluentResults.Error` with structured error information:

```csharp
// public IntegrationError(string code, string message, ErrorType type, Exception? exception = null)

var error = new IntegrationError(
    "Journal.NotFound",
    "Journal JBN-001 not found in company USMF",
    ErrorType.NotFound);

Result<LedgerJournalHeader> result = Result.Fail<LedgerJournalHeader>(error);
// result.IsFailed == true, result.GetError()?.Code == "Journal.NotFound"
```

Access properties directly: `error.Code`, `error.Message`, `error.Type`, `error.Exception`.

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

Creating errors for each type:

```csharp
var validation = new IntegrationError(
    "Validation.Error", "'DataAreaId' must not be empty", ErrorType.Validation);

var notFound = new IntegrationError(
    "OData.NotFound", "Entity not found for key: JBN-999", ErrorType.NotFound);

var conflict = new IntegrationError(
    "OData.Conflict", "Entity was modified by another process", ErrorType.Conflict);

var failure = new IntegrationError(
    "OData.RequestFailed", "HTTP 503 from D365", ErrorType.Failure,
    new HttpRequestException("Service Unavailable"));
// failure.Exception is the wrapped HttpRequestException
```

## Pattern Matching with Match()

**`Result<T>`** — `public static TOut Match<T, TOut>(this Result<T> result, Func<T, TOut> onSuccess, Func<IntegrationError, TOut> onFailure)`:

```csharp
IActionResult response = result.Match<LedgerJournalHeader, IActionResult>(
    onSuccess: header => new OkObjectResult(header),
    onFailure: error => error.Type switch
    {
        ErrorType.Validation => new BadRequestObjectResult(error.Message),
        ErrorType.NotFound   => new NotFoundObjectResult(error.Message),
        ErrorType.Conflict   => new ConflictObjectResult(error.Message),
        _                    => new StatusCodeResult(500)
    });
// IActionResult — 200 OK with entity, or 400/404/409/500 with error message
```

**Non-generic `Result`** (returned by batch and delete operations) — `public static TOut Match<TOut>(this Result result, Func<TOut> onSuccess, Func<IntegrationError, TOut> onFailure)`:

```csharp
Result batchResult = await mediator.Send(
    new DeleteCommand<LedgerJournalHeader>(entity), cancellationToken);

int statusCode = batchResult.Match(
    () => 204,
    error => error.Type switch
    {
        ErrorType.NotFound   => 404,
        ErrorType.Validation => 400,
        ErrorType.Conflict   => 409,
        _                    => 500
    }); // 204 on success, or 400/404/409/500 on failure
```

The `onFailure` callback receives the first `IntegrationError`. If no `IntegrationError` exists, a fallback error with code `"Unknown"` and type `ErrorType.Failure` is provided.

## Accessing a Wrapped Exception

```csharp
if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    if (error?.Exception is HttpRequestException httpEx)
        Console.WriteLine($"HTTP error: {httpEx.Message}"); // "HTTP error: Service Unavailable"
}
```

## See Also

- [[Validation]] — FluentValidation pipeline and `ErrorType.Validation`
- [[Commands]] — command error handling patterns
- [[Resilience]] — retry and circuit breaker for transient failures
- [[Testing]] — `HaveErrorCode()` and `HaveErrorType()` test assertions
