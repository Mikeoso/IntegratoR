# RELion

```csharp
services.AddRelionClient(context.Configuration); // registers IRelionService, auth handler, MediatR handlers

// Fetch journal lines from RELion
Result<List<RelionLedgerJournalLine>> result =
    await _relionService.GetNewJournalLinesAsync(DateTime.UtcNow.AddDays(-1), cancellationToken);
// result.Value -> List<RelionLedgerJournalLine>, auto-paginated (page size 500)
```

## Connection Setup

Add a `RelionSettings` section to `appsettings.json`. RELion supports `ApiKey` and `OAuth` authentication modes — see [[Configuration]] for the full settings table.

```json
{
  "RelionSettings": {
    "Url": "https://api.relion.example.com",
    "Timeout": 120,
    "Company": "My Company",
    "AuthMode": "ApiKey",
    "SubscriptionKey": "your-subscription-key",
    "SubscriptionHeaderKey": "Ocp-Apim-Subscription-Key"
  }
}
```

OAuth mode uses `ClientId`, `ClientSecret`, `TenantId`, and `Resource` instead of the subscription key fields.

Register in your host:

```csharp
using IntegratoR.RELion.Common.Extensions;

var host = new HostBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddRelionClient(context.Configuration);
    })
    .Build();
```

This registers `RelionSettings`, `RelionAuthenticationHandler`, an `HttpClient` named `"RelionApiClient"`, `IRelionService` (scoped), and all RELion MediatR handlers.

## IRelionService

Inject `IRelionService` via constructor injection. All methods return `Result<T>` from FluentResults.

### GetNewJournalLinesAsync

```csharp
// Task<Result<List<RelionLedgerJournalLine>>> GetNewJournalLinesAsync(DateTime since, CancellationToken ct)
DateTime since = DateTime.UtcNow.AddHours(-6);
Result<List<RelionLedgerJournalLine>> result =
    await _relionService.GetNewJournalLinesAsync(since, cancellationToken);
// result.Value.Count -> 42, all pages fetched automatically
```

### GetLedgerAccountMappingsAsync

```csharp
// Task<Result<RelionLedgerAccountMapping>> GetLedgerAccountMappingsAsync(int entryNo, CancellationToken ct)
Result<RelionLedgerAccountMapping> result =
    await _relionService.GetLedgerAccountMappingsAsync(entryNo: 1001, cancellationToken);
// result.Value.LedgerAccountNo -> "110180", result.Value.TaxAccountNo -> "251000"
// Returns success with empty strings if no mapping exists
```

### GetCompanyByNameAsync

```csharp
// Task<Result<RelionCompany>> GetCompanyByNameAsync(string companyName, CancellationToken ct)
Result<RelionCompany> result =
    await _relionService.GetCompanyByNameAsync("My Company", cancellationToken);
// result.Value.Id -> "42", result.Value.Name -> "My Company"
```

Both `GetNewJournalLinesAsync` and `GetLedgerAccountMappingsAsync` internally resolve the company using the `Company` value from `RelionSettings`.

## MediatR Queries

`GetRelionLedgerAccountMappingQuery` wraps the service call with automatic caching (30 minutes, keyed by `EntryNo`):

```csharp
using IntegratoR.RELion.Features.Queries.Ledger.GetLedgerAccountMapping;

var query = new GetRelionLedgerAccountMappingQuery(EntryNo: 1001);
Result<RelionLedgerAccountMapping> result = await mediator.Send(query, cancellationToken);
// result.Value.LedgerAccountNo -> "110180", cached for subsequent calls
```

## Error Handling

Failed results contain `IntegrationError` with typed error codes:

```csharp
if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    // error.Code -> "RelionService.GetLedgerAccountMappingsAsync.CompanyNotFound" | "Relion.ApiError" | "Relion.Exception"
    // error.Type -> ErrorType.NotFound | ErrorType.Failure
}
```

## See Also

- [[Configuration]] — `RelionSettings` reference and authentication modes
- [[Error-Handling]] — `IntegrationError` and `GetError()` pattern
- [[Getting-Started]] — DI registration for RELion
