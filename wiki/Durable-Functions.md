# Durable Functions

The Durable Functions isolated worker SDK uses **System.Text.Json** to serialise activity inputs
and outputs into the task hub. Without a custom converter, `Result<T>` cannot be deserialised on
replay and activities throw *"JSON value could not be converted to FluentResults.Result..."*.

**No setup required** — `services.AddIntegratoR(configuration)` automatically registers the
`Result<T>` converters with `DurableTaskWorkerOptions.DataConverter`. Activities and
orchestrators can return `Result<T>` and `Result` directly:

```csharp
// Program.cs — Result converters for Durable Functions are wired by AddIntegratoR
services.AddIntegratoR(context.Configuration, integrator =>
{
    integrator.AddConsumerHandlers(clientAssembly);
});
```

The registration is lazy: consumers not using Durable Functions never resolve
`DurableTaskWorkerOptions` and pay zero runtime cost. Consumers who want to customise the
data converter further can call `services.Configure<DurableTaskWorkerOptions>(...)` after
`AddIntegratoR` — the last configurator wins.

## Two JSON serialisers in this project

IntegratoR uses **two** JSON serialisers and `Result<T>` needs converters in both:

- **System.Text.Json** — the Durable Functions isolated worker SDK and `DistributedCacheService`.
  Configured via `DurableTaskWorkerOptions.DataConverter` (above) and
  `DistributedCacheService.SerializerOptions`. STJ converters live in
  `IntegratoR.Abstractions/Common/Results/SystemText/`.
- **Newtonsoft.Json** — the RELion API client (`[JsonProperty]` attributes,
  `JsonConvert.DeserializeObject`) and HTTP trigger payloads. Configured globally via
  `JsonConvert.DefaultSettings` in `Program.cs`. Newtonsoft converters live in
  `IntegratoR.Abstractions/Common/Results/`.

Both converter families delegate to the serialiser-agnostic `ResultJsonShape` helper for
property names and the `IError ↔ (code, message, type)` mapping, so the JSON shape stays
in lockstep across both.

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
