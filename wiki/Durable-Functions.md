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

> **If you replace `DataConverter` with your own, call `jsonOptions.AddResultConverters()`
> on the underlying `JsonSerializerOptions` to retain `Result<T>` round-tripping.** A
> consumer who installs a fresh `JsonDataConverter(jsonOptions)` without this call loses
> the auto-wired Result converters and reintroduces the original *"JSON value could not be
> converted to FluentResults.Result..."* failure on activity replay.

## Two JSON serialisers in this project

IntegratoR uses **two** JSON serialisers and `Result<T>` needs converters in both:

- **System.Text.Json** — the Durable Functions isolated worker SDK and `DistributedCacheService`.
  Configured via `DurableTaskWorkerOptions.DataConverter` (above) and
  `DistributedCacheService.SerializerOptions`. STJ converters live in
  `IntegratoR.Abstractions/Common/Results/SystemText/`.
- **Newtonsoft.Json** — the RELion API client (`[JsonProperty]` attributes,
  `JsonConvert.DeserializeObject`) and any HTTP trigger payloads that opt into Newtonsoft.
  Configured globally via `JsonConvert.DefaultSettings` in your host's `Program.cs`.
  Newtonsoft converters live in `IntegratoR.Abstractions/Common/Results/`.

Both converter families delegate to the serialiser-agnostic `ResultJsonShape` helper for
property names and the `IError ↔ (code, message, type)` mapping, so the JSON shape stays
in lockstep across both.

## Activity Functions

Activity functions wrap MediatR calls, returning `Result<T>` through orchestration state:

```csharp
public sealed class LedgerJournalActivityFunctions
{
    private readonly IMediator _mediator;

    public LedgerJournalActivityFunctions(IMediator mediator) => _mediator = mediator;

    [Function(nameof(CreateJournalHeader))]
    public async Task<Result<LedgerJournalHeader>> CreateJournalHeader(
        [ActivityTrigger] LedgerJournalHeader header,
        CancellationToken cancellationToken)
    {
        CreateCommand<LedgerJournalHeader> command = new(header);
        return await _mediator.Send(command, cancellationToken).ConfigureAwait(false);
    }

    [Function(nameof(CreateJournalLine))]
    public async Task<Result<LedgerJournalLine>> CreateJournalLine(
        [ActivityTrigger] LedgerJournalLine line,
        CancellationToken cancellationToken)
    {
        CreateCommand<LedgerJournalLine> command = new(line);
        return await _mediator.Send(command, cancellationToken).ConfigureAwait(false);
    }
}
```

## Fan-Out/Fan-In Orchestration

An orchestrator creates a header, then fans out to create lines in parallel and fans in to collect results:

```csharp
public sealed class JournalOrchestrator
{
    [Function(nameof(CreateJournalOrchestration))]
    public async Task<Result<string>> CreateJournalOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        LedgerJournalHeader header = new()
        {
            DataAreaId = "USMF",
            JournalName = "GenJrn",
            Description = "Orchestrated journal"
        };

        Result<LedgerJournalHeader> headerResult = await context.CallActivityAsync<Result<LedgerJournalHeader>>(
            nameof(LedgerJournalActivityFunctions.CreateJournalHeader), header);

        if (headerResult.IsFailed)
        {
            return Result.Fail<string>(headerResult.Errors); // propagate activity errors
        }

        string journalBatchNumber = headerResult.Value.JournalBatchNumber!;

        // Fan out — create lines in parallel
        List<LedgerJournalLine> lines =
        [
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
        ];

        IEnumerable<Task<Result<LedgerJournalLine>>> lineTasks = lines.Select(line =>
            context.CallActivityAsync<Result<LedgerJournalLine>>(
                nameof(LedgerJournalActivityFunctions.CreateJournalLine), line));

        // Fan in — await all, aggregate failures
        Result<LedgerJournalLine>[] lineResults = await Task.WhenAll(lineTasks);

        List<Result<LedgerJournalLine>> failures = lineResults.Where(r => r.IsFailed).ToList();
        if (failures.Count > 0)
        {
            return Result.Fail<string>(failures.SelectMany(f => f.Errors));
        }

        return Result.Ok(journalBatchNumber);
    }
}
```

> The example classes above (`LedgerJournalActivityFunctions`, `JournalOrchestrator`) are illustrative
> sketches — they live in your consuming project, not in the framework. The classes you
> define against `IMediator` and the generic `CreateCommand<T>` type are enough to get
> `Result<T>` round-tripping through the task hub.

## HTTP Starter

```csharp
public sealed class JournalStarter
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

- [[Azure-Functions-Host]] — host setup and `AddIntegratoR` composition
- [[Error-Handling]] — `Result<T>` pattern and `IntegrationError`
- [[D365-FO-Journals]] — journal entities used in orchestrations
- [[Batch-Operations]] — alternative to fan-out for bulk operations
