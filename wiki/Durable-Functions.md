# Durable Functions

```csharp
// Program.cs — register Result converters for Durable Functions serialisation
JsonConvert.DefaultSettings = () => new JsonSerializerSettings
{
    Converters = { new ResultJsonConverter(), new ResultGenericJsonConverter() }
};
```

Without these converters, `Result<T>` loses error metadata after orchestration replay.

## Activity Functions

Activity functions wrap MediatR calls, returning `Result<T>` through orchestration state:

```csharp
public class JournalActivities
{
    private readonly IMediator _mediator;

    public JournalActivities(IMediator mediator) => _mediator = mediator;

    [Function(nameof(CreateJournalHeader))]
    public async Task<Result<LedgerJournalHeader>> CreateJournalHeader(
        [ActivityTrigger] LedgerJournalHeader header,
        CancellationToken cancellationToken)
    {
        var command = new CreateLedgerJournalHeaderCommand<LedgerJournalHeader>(header);
        return await _mediator.Send(command, cancellationToken);
    }

    [Function(nameof(CreateJournalLine))]
    public async Task<Result<LedgerJournalLine>> CreateJournalLine(
        [ActivityTrigger] LedgerJournalLine line,
        CancellationToken cancellationToken)
    {
        var command = new CreateLedgerJournalLineCommand<LedgerJournalLine>(line);
        return await _mediator.Send(command, cancellationToken);
    }
}
```

## Fan-Out/Fan-In Orchestration

The orchestrator creates a header, then fans out to create lines in parallel and fans in to collect results:

```csharp
public class JournalOrchestrator
{
    [Function(nameof(CreateJournalOrchestration))]
    public async Task<Result<string>> CreateJournalOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var header = new LedgerJournalHeader
        {
            DataAreaId = "USMF",
            JournalName = "GenJrn",
            Description = "Orchestrated journal"
        };

        Result<LedgerJournalHeader> headerResult = await context.CallActivityAsync<Result<LedgerJournalHeader>>(
            nameof(JournalActivities.CreateJournalHeader), header);

        if (headerResult.IsFailed)
            return Result.Fail<string>(headerResult.Errors); // propagate activity errors

        string journalBatchNumber = headerResult.Value.JournalBatchNumber!;

        // Fan out — create lines in parallel
        var lines = new List<LedgerJournalLine>
        {
            new()
            {
                DataAreaId = "USMF",
                JournalBatchNumber = journalBatchNumber,
                AccountDisplayValue = "110180-001-027--",
                AccountType = LedgerJournalACType.Ledger,
                CurrencyCode = "USD",
                TransDate = new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero),
                DebitAmount = 1500.00m
            },
            new()
            {
                DataAreaId = "USMF",
                JournalBatchNumber = journalBatchNumber,
                AccountDisplayValue = "170150-001-027--",
                AccountType = LedgerJournalACType.Ledger,
                CurrencyCode = "USD",
                TransDate = new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero),
                CreditAmount = 1500.00m
            }
        };

        var lineTasks = lines.Select(line =>
            context.CallActivityAsync<Result<LedgerJournalLine>>(
                nameof(JournalActivities.CreateJournalLine), line));

        // Fan in — await all, aggregate failures
        Result<LedgerJournalLine>[] lineResults = await Task.WhenAll(lineTasks);

        var failures = lineResults.Where(r => r.IsFailed).ToList();
        if (failures.Any())
            return Result.Fail<string>(failures.SelectMany(f => f.Errors));

        return Result.Ok(journalBatchNumber);
    }
}
```

## HTTP Starter

```csharp
public class JournalStarter
{
    [Function(nameof(StartJournalOrchestration))]
    public async Task<HttpResponseData> StartJournalOrchestration(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(JournalOrchestrator.CreateJournalOrchestration)); // returns unique instance ID

        return client.CreateCheckStatusResponse(req, instanceId); // 202 Accepted with status query URLs
    }
}
```

## Error Handling

Errors flow through `Result<T>` rather than exceptions. Always return `Result.Fail` from activities instead of throwing — this preserves structured error information via [[Error-Handling]]:

```csharp
// Prefer returning Result.Fail (keeps IntegrationError metadata):
return Result.Fail<LedgerJournalHeader>(new IntegrationError("Code", "Message", ErrorType.Failure));

// Avoid throwing (loses structured error context):
throw new InvalidOperationException("Something went wrong");
```

## See Also

- [[Azure-Functions-Host]] — host setup with `ResultJsonConverter` registration
- [[Error-Handling]] — `Result<T>` pattern and `IntegrationError`
- [[D365-FO-Journals]] — journal entities used in orchestrations
- [[Batch-Operations]] — alternative to fan-out for bulk operations
