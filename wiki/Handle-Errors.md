# Handle Errors

The framework uses `FluentResults.Result<T>` as the return shape for every operation that can fail at the business level. Consumer code never wraps framework calls in `try`/`catch` for business-logic errors — the `Result` carries success state, an `IntegrationError` on failure, and an optional original exception.

## Check a Result

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(header),
    cancellationToken).ConfigureAwait(false);

if (result.IsSuccess)
{
    LedgerJournalHeader created = result.Value;
    logger.LogInformation("Created journal {BatchNumber}", created.JournalBatchNumber);
    return;
}

IntegrationError? error = result.GetError();
logger.LogWarning("Create failed: [{Code}] {Message}", error?.Code, error?.Message);
```

`GetError()` is an extension method (in `IntegratoR.Abstractions.Common.Results`) that returns the first `IntegrationError` from the result's error list, or `null` if no `IntegrationError` exists. Always prefer this over manually searching `result.Errors`.

## Pattern-Match with `Match()`

```csharp
string message = result.Match(
    onSuccess: header => $"Created {header.JournalBatchNumber}",
    onFailure: error => $"Failed: [{error.Code}] {error.Message}");
```

The non-generic overload handles `Result` (no value):

```csharp
string message = batchResult.Match(
    onSuccess: () => "Batch complete",
    onFailure: error => $"Batch failed: {error.Message}");
```

`Match` is exhaustive — both delegates must be supplied. If the failed result somehow lacks an `IntegrationError`, the overload synthesises a fallback `IntegrationError("Unknown", <message>, ErrorType.Failure)` so the `onFailure` delegate always receives a non-null error.

## The `IntegrationError` Type

`IntegrationError` extends `FluentResults.Error` with three additional properties:

```csharp
public class IntegrationError : FluentResults.Error
{
    public string Code { get; }            // machine-readable, e.g. "OData.AuthenticationFailed"
    public ErrorType Type { get; }         // category for HTTP mapping + log-level selection
    public Exception? Exception { get; }   // original exception, if the failure wrapped one
}
```

`Code` is freeform — the framework uses dotted-segment codes like `OData.NotFound`, `Validation.Error`, `DimensionParameters.NotFound`. Custom handlers should follow the same convention so HTTP responses and log aggregators can match on prefix.

The optional `Exception` is set when the failure originated from an exception (e.g. `ODataClientException`, `MsalServiceException`). The constructor automatically calls `CausedBy(exception)` so `result.Errors[0].Reasons` also surfaces the exception for downstream FluentResults consumers.

## The `ErrorType` Enum

`ErrorType` categorises errors for HTTP status mapping and log-level selection:

| ErrorType | Meaning | Typical HTTP status | Typical log level |
|---|---|---|---|
| `Failure` | General failure (default) | 500 Internal Server Error | Error |
| `Validation` | Input validation failed in the MediatR pipeline | 400 Bad Request | Warning |
| `NotFound` | Entity not found by composite key | 404 Not Found | Information |
| `Conflict` | Duplicate, concurrency conflict, or domain rule violation | 409 Conflict | Warning |

A typical Azure Function handler switches on `error.Type`:

```csharp
HttpResponseData response = error?.Type switch
{
    ErrorType.Validation => req.CreateResponse(HttpStatusCode.BadRequest),
    ErrorType.NotFound   => req.CreateResponse(HttpStatusCode.NotFound),
    ErrorType.Conflict   => req.CreateResponse(HttpStatusCode.Conflict),
    _                    => req.CreateResponse(HttpStatusCode.InternalServerError)
};
await response.WriteAsJsonAsync(new { error?.Code, error?.Message }, cancellationToken)
    .ConfigureAwait(false);
return response;
```

> Never copy `error.Message` raw into the HTTP `ReasonPhrase` — internal codes (tenant IDs, MSAL error codes, D365 inner-error payloads) can leak. Surface a generic message externally and keep the full error in the structured log.

## Creating Custom Errors

Custom command and query handlers return errors using `Result.Fail`:

```csharp
return Result.Fail<LedgerJournalHeader>(new IntegrationError(
    code: "Journal.AlreadyPosted",
    message: "Cannot modify a posted journal.",
    type: ErrorType.Conflict));
```

Wrap an existing exception to preserve the stack:

```csharp
catch (ODataClientException ex)
{
    return Result.Fail<LedgerJournalHeader>(new IntegrationError(
        code: "OData.RequestFailed",
        message: ex.Message,
        type: ErrorType.Failure,
        exception: ex));
}
```

To propagate errors from a downstream call verbatim — preserving every `IError` (including `IntegrationError`) — use the constructor overload that takes a list:

```csharp
Result<IEnumerable<LedgerJournalHeader>> upstream = await _service.FindAsync(...);
if (upstream.IsFailed)
{
    return Result.Fail<DimensionFormat>(upstream.Errors);
}
```

The handler in `GetDimensionOrdersQueryHandler` uses this pattern so callers see the real underlying cause (auth failure, entity-set-not-found, APIM rejection) rather than a generic wrapper.

## Validation Errors

Validation failures emitted by the `ValidationBehaviour` use a fixed shape:

```csharp
new IntegrationError(
    code: "Validation.Error",
    message: <first validation failure message>,
    type: ErrorType.Validation);
```

The behaviour returns the **first** validation failure rather than the full collection. This keeps client-side handling simple — most clients only care about the first reason. If a validator surfaces multiple rules, only the first reaches the client; the rest are dropped.

## Exceptions That Still Throw

The `Result` pattern covers business-logic failures. True infrastructure exceptions still propagate:

- Network failures the Polly retry policy cannot recover from
- `NullReferenceException` from genuine bugs
- `OperationCanceledException` from the cancellation token

The `LoggingBehaviour` catches unhandled exceptions, logs them at `Error` level with the request payload, and re-throws. Polly handles transient HTTP errors before they reach the pipeline (see [Configure Resilience](Configure-Resilience)).

## Format an Error for Logging

A short extension that produces a single-line summary suitable for `LogWarning`/`LogError`:

```csharp
string Format(IResultBase result)
{
    if (result.IsSuccess)
        return "Success";

    IntegrationError? error = result.GetError();
    return error is not null
        ? $"[{error.Type} {error.Code}] {error.Message}"
        : (result.Errors.FirstOrDefault()?.Message ?? "Unknown error");
}
```

For structured logging (App Insights, Seq) prefer separate parameters so `ErrorType` and `ErrorCode` remain queryable:

```csharp
_logger.LogWarning(
    error?.Exception,
    "{Operation} failed: [{ErrorType} {ErrorCode}] {ErrorMessage}",
    nameof(CreateLedgerJournalHeader),
    error?.Type, error?.Code, error?.Message);
```

## See Also

- [Send Commands](Send-Commands) — command return shapes
- [Run Queries](Run-Queries) — query return shapes (`GetByKeyQuery` returns `NotFound` on miss)
- [Add Validation](Add-Validation) — how `Validation.Error` is produced
- [Configure Resilience](Configure-Resilience) — when retries swallow vs surface errors
- [Troubleshoot Common Issues](Troubleshoot-Common-Issues) — common error codes and their resolutions
