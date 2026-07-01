# Handle Errors
> Last verified against v2.0.1

Every operation that can fail returns `FluentResults.Result<T>`. Business failures never throw — the result carries success state, and on failure an `IntegrationError` with a machine-readable `Code`, an `ErrorType`, and the original `Exception` when one was wrapped. Inspect the result rather than wrapping calls in `try`/`catch`.

```csharp
var header = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalName = "GenJrn",
    Description = "April accruals",
};

Result<LedgerJournalHeader> result = await mediator
    .Send(new CreateCommand<LedgerJournalHeader>(header), cancellationToken)
    .ConfigureAwait(false);

if (result.IsSuccess)
{
    LedgerJournalHeader created = result.Value;
    logger.LogInformation("Created journal {BatchNumber}", created.JournalBatchNumber);
}
else
{
    IntegrationError? error = result.GetError();
    logger.LogWarning("Create failed: [{Type} {Code}]", error?.Type, error?.Code);
}
```

`result.GetError()` returns the first `IntegrationError` from the result, or `null` if the result carries none. Use it consistently — never hand-roll a LINQ query over `result.Errors`.

## Read the error

`IntegrationError` extends `FluentResults.Error` with three members. Deep-linked source: [IntegrationError.cs](https://github.com/Mikeoso/IntegratoR/blob/main/IntegratoR.Abstractions/Common/Results/IntegrationError.cs).

| Member | What it holds |
|---|---|
| `Code` | Machine-readable, dotted-segment string — `Validation.Error`, `Auth.Msal.invalid_client`. Match on the prefix. |
| `Type` | An `ErrorType` for HTTP-status and log-level mapping. |
| `Exception` | The wrapped exception when the failure originated from one; otherwise `null`. |

When an exception is passed, the constructor calls `CausedBy(exception)`, so FluentResults' own `Reasons` chain surfaces it too.

## The four `ErrorType` values

`ErrorType` has exactly four members. There are no others.

| `ErrorType` | Meaning | Typical HTTP status |
|---|---|---|
| `Failure` | General or unexpected failure (the default) | 500 |
| `Validation` | Input rejected by the `ValidationBehaviour` | 400 |
| `NotFound` | No entity matched the composite key | 404 |
| `Conflict` | Duplicate, concurrency clash, or domain-rule breach | 409 |

Map to an HTTP response by switching on `Type`:

```csharp
IntegrationError? error = result.GetError();

HttpResponseData response = error?.Type switch
{
    ErrorType.Validation => req.CreateResponse(HttpStatusCode.BadRequest),
    ErrorType.NotFound   => req.CreateResponse(HttpStatusCode.NotFound),
    ErrorType.Conflict   => req.CreateResponse(HttpStatusCode.Conflict),
    _                    => req.CreateResponse(HttpStatusCode.InternalServerError),
};
```

## Pattern-match with `Match`

`Match` collapses both branches into one expression and always hands `onFailure` a non-null `IntegrationError`. If a failed result somehow carries no `IntegrationError`, `Match` synthesises `IntegrationError("Unknown", <message>, ErrorType.Failure)` so your delegate never dereferences null.

```csharp
string summary = result.Match(
    onSuccess: created => $"Created {created.JournalBatchNumber}",
    onFailure: error => $"Failed: [{error.Type} {error.Code}]");
```

The non-generic overload matches a valueless `Result` from a batch command:

```csharp
Result batchResult = await mediator
    .Send(new CreateBatchCommand<LedgerJournalLine>(lines), cancellationToken)
    .ConfigureAwait(false);

string summary = batchResult.Match(
    onSuccess: () => "Batch complete",
    onFailure: error => $"Batch failed: {error.Code}");
```

## Concrete D365 rejections

Each downstream rejection reaches you as a failed `Result<T>` with a distinct `Code` and `Type`. None throws.

### Read-only field on update — 403

D365 marks several `LedgerJournalHeader` fields read-only after create: `JournalName`, `IsPosted`, `JournalTotalDebit`, `JournalTotalCredit`, and `AccountingCurrency`. Each carries `[ODataField(IgnoreOnUpdate = true)]`, so the serialiser drops it from the PATCH payload.

> [!WARNING]
> If even one `IgnoreOnUpdate` field reaches the payload, D365 rejects the **whole** PATCH with HTTP 403 (`ODataSecurityException`, `"update not allowed for field 'X'"`) — not only that field. Audit every field against D365's update semantics before shipping an entity. The failed `Result<T>` surfaces as a `Failure`-typed `IntegrationError` whose `Exception` holds the client exception.

```csharp
Result<LedgerJournalHeader> result = await mediator
    .Send(new UpdateCommand<LedgerJournalHeader>(header), cancellationToken)
    .ConfigureAwait(false);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    // error.Type == ErrorType.Failure; error.Exception carries the ODataClientException (HTTP 403).
    logger.LogError(error?.Exception, "Update rejected: [{Code}]", error?.Code);
}
```

### Validation — 400

The `ValidationBehaviour` runs before the handler and returns the **first** validation failure as a fixed shape — `IntegrationError("Validation.Error", <first message>, ErrorType.Validation)`. Later failures are dropped. See [Add Validation](Add-Validation).

```csharp
if (result.IsFailed && result.GetError()?.Type == ErrorType.Validation)
{
    // Code is always "Validation.Error"; Message is the first FluentValidation failure.
    return req.CreateResponse(HttpStatusCode.BadRequest);
}
```

### Authentication — 401

An OAuth token-acquisition failure returns `IntegrationError("Auth.Msal.{code}", "Token acquisition failed", ErrorType.Failure, ex)`, where `{code}` is the MSAL error code and `ex` is the MSAL exception. On the HTTP path the `ODataAuthenticationHandler` short-circuits with a 401 whose `ReasonPhrase` is the fixed string `"Authentication failed"`. See [Authentication Modes](Authentication-Modes).

> [!WARNING]
> Never copy `error.Message` or `error.Exception` detail into an HTTP `ReasonPhrase` or response body — tenant IDs, MSAL/AADSTS codes, and D365 inner-error payloads leak that way. Return a generic message to the caller and log the full error server-side only.

## Raise an error from a custom handler

Return a failed result with `Result.Fail` — do not throw for a business rule:

```csharp
return Result.Fail<LedgerJournalHeader>(new IntegrationError(
    code: "Journal.AlreadyPosted",
    message: "Cannot modify a posted journal.",
    type: ErrorType.Conflict));
```

Pass the exception when wrapping one, so the stack is preserved:

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

To propagate a downstream failure verbatim — keeping every `IError`, including the original `IntegrationError` — pass the error list straight through:

```csharp
Result<IEnumerable<LedgerJournalHeader>> upstream = await service
    .FindAsync(filter, cancellationToken)
    .ConfigureAwait(false);

if (upstream.IsFailed)
{
    return Result.Fail<DimensionFormat>(upstream.Errors);
}
```

## What still throws

`Result<T>` covers business-logic failures. Genuine infrastructure faults still propagate as exceptions: transient network faults Polly cannot recover, `NullReferenceException` from a real bug, and `OperationCanceledException` from the cancellation token. The `LoggingBehaviour` logs an unhandled exception at `Error` level and re-throws; Polly absorbs transient HTTP faults before they reach the pipeline. See [Configure Resilience](Configure-Resilience).

## See Also

- [Send Commands](Send-Commands)
- [Run Queries](Run-Queries)
- [Add Validation](Add-Validation)
- [Troubleshoot Common Issues](Troubleshoot-Common-Issues)
