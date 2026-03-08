# Query RELion Data

Fetch journal lines and ledger account mappings from the RELion API using either `IRelionService` directly or MediatR queries.

> **Prerequisites:** [[Configure-the-RELion-Connection]]

## Fetch New Journal Lines via IRelionService

```csharp
using FluentResults;
using IntegratoR.RELion.Domain.Models;
using IntegratoR.RELion.Interfaces.Services;

// Inject IRelionService via constructor injection
public class JournalSyncService
{
    private readonly IRelionService _relionService;

    public JournalSyncService(IRelionService relionService)
    {
        _relionService = relionService;
    }

    public async Task SyncJournalLines(CancellationToken cancellationToken)
    {
        DateTime since = DateTime.UtcNow.AddDays(-1);

        Result<List<RelionLedgerJournalLine>> result =
            await _relionService.GetNewJournalLinesAsync(since, cancellationToken);

        if (result.IsSuccess)
        {
            List<RelionLedgerJournalLine> lines = result.Value;
            Console.WriteLine($"Fetched {lines.Count} journal lines since {since:O}");
        }
    }
}
```

```text
Fetched 42 journal lines since 2026-03-07T00:00:00.0000000Z
```

The service automatically handles pagination internally, fetching all pages of results.

## Fetch Ledger Account Mappings via IRelionService

```csharp
Result<RelionLedgerAccountMapping> mappingResult =
    await _relionService.GetLedgerAccountMappingsAsync(entryNo: 1001, cancellationToken);

if (mappingResult.IsSuccess)
{
    RelionLedgerAccountMapping mapping = mappingResult.Value;
    Console.WriteLine($"Ledger: {mapping.LedgerAccountNo}, Tax: {mapping.TaxAccountNo}");
}
```

```text
Ledger: 110180, Tax: 251000
```

If no mapping exists for the given `entryNo`, the service returns a successful result with empty strings:

```text
Ledger: , Tax:
```

## Use MediatR Queries for Ledger Account Mappings

The `GetRelionLedgerAccountMappingQuery` wraps the service call and adds automatic caching for 30 minutes:

```csharp
using IntegratoR.RELion.Features.Queries.Ledger.GetLedgerAccountMapping;
using MediatR;

var query = new GetRelionLedgerAccountMappingQuery(EntryNo: 1001);

Result<RelionLedgerAccountMapping> result = await mediator.Send(query, cancellationToken);

if (result.IsSuccess)
{
    Console.WriteLine($"Ledger: {result.Value.LedgerAccountNo}");
}
```

```text
Ledger: 110180
```

The query implements `ICacheableQuery<Result<RelionLedgerAccountMapping>>` with:
- **Cache key:** the `EntryNo` value (e.g. `"1001"`)
- **Cache duration:** 30 minutes

Subsequent calls with the same `EntryNo` within 30 minutes return the cached result without calling the RELion API.

## Look Up a Company

```csharp
Result<RelionCompany> companyResult =
    await _relionService.GetCompanyByNameAsync("My Company", cancellationToken);

if (companyResult.IsSuccess)
{
    Console.WriteLine($"Company ID: {companyResult.Value.Id}, Name: {companyResult.Value.Name}");
}
```

```text
Company ID: 42, Name: My Company
```

## When Things Go Wrong

**Company not found** — all data-fetching methods look up the configured company first. If the company does not exist:

```csharp
// GetNewJournalLinesAsync also fails if the company lookup fails
Result<List<RelionLedgerJournalLine>> result =
    await _relionService.GetNewJournalLinesAsync(DateTime.UtcNow.AddDays(-1), cancellationToken);

// result.IsFailed == true
// Error code: "RelionService.GetLedgerAccountMappingsAsync.CompanyNotFound"
// Error message: "Failed to retrieve company information for My Company."
```

**API error** — if the RELion API returns a non-success status code:

```csharp
// result.IsFailed == true
// Error code: "Relion.ApiError"
// Error message: "API returned status code BadRequest."
```

**Network or unexpected errors** — caught and wrapped in a `Result.Fail`:

```csharp
// result.IsFailed == true
// Error code: "Relion.Exception"
// Error message contains the exception message
```

## See Also

- [[Configure-the-RELion-Connection]] — set up authentication and settings
- [[Create-a-Ledger-Journal]] — use fetched data to create journals in F&O
- [[Set-Up-an-Azure-Functions-Host]] — register RELion in the host
