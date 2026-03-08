# RelionService

Service implementation for fetching data from the RELion API. Provides methods for retrieving journal lines, ledger account mappings, and company information with built-in pagination.

## Use the Service

```csharp
// Inject via DI (registered by AddRelionClient)
public class MyFunction
{
    private readonly IRelionService _relionService;

    public MyFunction(IRelionService relionService)
    {
        _relionService = relionService;
    }

    public async Task ProcessNewLines()
    {
        DateTime since = DateTime.UtcNow.AddDays(-1);
        Result<List<RelionLedgerJournalLine>> result =
            await _relionService.GetNewJournalLinesAsync(since, cancellationToken);
        // Result: Result<List<RelionLedgerJournalLine>> — all lines since timestamp, auto-paginated

        if (result.IsSuccess)
        {
            List<RelionLedgerJournalLine> lines = result.Value;
            // Process lines...
        }
    }
}
```

## IRelionService Methods

### GetNewJournalLinesAsync

Fetches journal lines created or modified since a given timestamp. Automatically pages through all results using a page size of 500.

```csharp
Task<Result<List<RelionLedgerJournalLine>>> GetNewJournalLinesAsync(
    DateTime since,
    CancellationToken cancellationToken = default)
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `since` | `DateTime` | Yes | Timestamp to fetch records from (ISO 8601 format) |
| `cancellationToken` | `CancellationToken` | No | Cancellation token |

**Returns:** `Result<List<RelionLedgerJournalLine>>` containing all matching journal lines across all pages.

```csharp
DateTime since = DateTime.UtcNow.AddHours(-6);
Result<List<RelionLedgerJournalLine>> result =
    await _relionService.GetNewJournalLinesAsync(since, cancellationToken);
// Result: Result<List<RelionLedgerJournalLine>> — all lines since timestamp

if (result.IsSuccess)
{
    Console.WriteLine($"Retrieved {result.Value.Count} lines");
    foreach (RelionLedgerJournalLine line in result.Value)
    {
        // Process each line
    }
}
```

### GetLedgerAccountMappingsAsync

Retrieves a ledger account mapping by entry number.

```csharp
Task<Result<RelionLedgerAccountMapping>> GetLedgerAccountMappingsAsync(
    int entryNo,
    CancellationToken cancellationToken = default)
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `entryNo` | `int` | Yes | The entry number to look up |
| `cancellationToken` | `CancellationToken` | No | Cancellation token |

**Returns:** `Result<RelionLedgerAccountMapping>` containing the mapping. If no mapping is found, returns a success result with empty `LedgerAccountNo` and `TaxAccountNo`.

```csharp
Result<RelionLedgerAccountMapping> result =
    await _relionService.GetLedgerAccountMappingsAsync(42, cancellationToken);
// Result: Result<RelionLedgerAccountMapping> — mapping or success with empty strings if not found

if (result.IsSuccess)
{
    string ledgerAccount = result.Value.LedgerAccountNo;
    string taxAccount = result.Value.TaxAccountNo;
}
```

### GetCompanyByNameAsync

Retrieves company details by name from the RELion API.

```csharp
Task<Result<RelionCompany>> GetCompanyByNameAsync(
    string companyName,
    CancellationToken cancellationToken = default)
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `companyName` | `string` | Yes | Company name to look up (case-insensitive) |
| `cancellationToken` | `CancellationToken` | No | Cancellation token |

**Returns:** `Result<RelionCompany>` containing the company details.

```csharp
Result<RelionCompany> result =
    await _relionService.GetCompanyByNameAsync("USMF", cancellationToken);
// Result: Result<RelionCompany> — company details or failure if not found

if (result.IsSuccess)
{
    string companyId = result.Value.Id;
}
```

## Handle Errors

All methods return `Result<T>` from FluentResults. Errors are typed as `IntegrationError`.

### Company Not Found

`GetNewJournalLinesAsync` and `GetLedgerAccountMappingsAsync` internally resolve the company by name. If the company does not exist:

```csharp
Result<List<RelionLedgerJournalLine>> result =
    await _relionService.GetNewJournalLinesAsync(DateTime.UtcNow.AddDays(-1), cancellationToken);

if (result.IsFailed)
{
    IntegrationError error = result.Errors.OfType<IntegrationError>().First();
    // error.Code    -> "RelionService.GetLedgerAccountMappingsAsync.CompanyNotFound"
    // error.Message -> "Failed to retrieve company information for USMF."
    // error.Type    -> ErrorType.Failure
}
```

### API Errors

HTTP errors from the RELion API surface as failed results:

```csharp
Result<RelionCompany> result =
    await _relionService.GetCompanyByNameAsync("INVALID", cancellationToken);

if (result.IsFailed)
{
    IntegrationError error = result.Errors.OfType<IntegrationError>().First();
    // error.Code    -> "Relion.CompanyNotFound"
    // error.Message -> "Company with name 'INVALID' not found."
    // error.Type    -> ErrorType.NotFound
}
```

### Unexpected Exceptions

Exceptions (network failures, deserialisation errors) are caught and wrapped in `IntegrationError`:

```csharp
// error.Code    -> "Relion.Exception"
// error.Message -> exception message
// error.Type    -> ErrorType.Failure
```

## Understand the Internal Behaviour

### Pagination

`GetNewJournalLinesAsync` pages through results automatically:
- Page size: 500 records
- Continues fetching while `MoreRows` is `true` in the response
- Accumulates all results into a single list

### Company Resolution

Both `GetNewJournalLinesAsync` and `GetLedgerAccountMappingsAsync` resolve the company internally using `GetCompanyByNameAsync` with the `Company` value from `RelionSettings`.

### API Endpoint

The service uses the RELion Universal API endpoint:
```
/api/aareon/universalapi/v1.0/companies({companyId})/universalRequests?$expand=entitySet
```

## Constructor

```csharp
public RelionService(
    IHttpClientFactory httpClientFactory,
    ILogger<RelionService> logger,
    IOptions<RelionSettings> settings)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `httpClientFactory` | `IHttpClientFactory` | Factory for the named "RelionApiClient" HTTP client |
| `logger` | `ILogger<RelionService>` | Structured logger |
| `settings` | `IOptions<RelionSettings>` | RELion configuration settings |

> You do not construct `RelionService` directly. It is resolved via DI as `IRelionService`.

## See Also

- [[API-RelionSettings]] — connection and authentication configuration
- [[Query-RELion-Data]] — step-by-step guide for querying RELion
- [[Configure-the-RELion-Connection]] — setup guide
