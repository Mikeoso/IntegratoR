# Build a Durable Functions Orchestration

Create a fan-out/fan-in orchestration that uses IntegratoR MediatR commands and queries, with `Result<T>` flowing through orchestration state.

> **Prerequisites:** [[Set-Up-an-Azure-Functions-Host]]

## Set Up JSON Serialisation for Result Types

Durable Functions serialises all orchestration inputs, outputs, and intermediate state using `Newtonsoft.Json`. The `ResultJsonConverter` and `ResultGenericJsonConverter` must be registered at startup so `Result` and `Result<T>` survive round-trip serialisation:

```csharp
using IntegratoR.Abstractions.Common.Results;
using Newtonsoft.Json;

// In Program.cs — before building the host
JsonConvert.DefaultSettings = () => new JsonSerializerSettings
{
    Converters = { new ResultJsonConverter(), new ResultGenericJsonConverter() }
};
```

Without these converters, Durable Functions silently drops the error metadata from failed results, making failure diagnosis impossible.

## Define the Activity Functions

Activity functions wrap MediatR calls. Each returns `Result<T>` which is automatically serialised by the Durable Functions runtime:

```csharp
using FluentResults;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;
using MediatR;
using Microsoft.Azure.Functions.Worker;

namespace MyApp.Functions;

public class JournalActivities
{
    private readonly IMediator _mediator;

    public JournalActivities(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Function(nameof(CreateJournalHeader))]
    public async Task<Result<LedgerJournalHeader>> CreateJournalHeader(
        [ActivityTrigger] LedgerJournalHeader header,
        CancellationToken cancellationToken)
    {
        var command = new CreateLedgerJournalHeaderCommand<LedgerJournalHeader>(header);
        return await _mediator.Send(command, cancellationToken);
        // Result: Result<LedgerJournalHeader> — Success with server-generated JournalBatchNumber
    }

    [Function(nameof(CreateJournalLine))]
    public async Task<Result<LedgerJournalLine>> CreateJournalLine(
        [ActivityTrigger] LedgerJournalLine line,
        CancellationToken cancellationToken)
    {
        var command = new CreateLedgerJournalLineCommand<LedgerJournalLine>(line);
        return await _mediator.Send(command, cancellationToken);
        // Result: Result<LedgerJournalLine> — Success with server-generated LineNumber
    }
}
```

## Write the Orchestrator

The orchestrator creates a header, then fans out to create multiple lines in parallel:

```csharp
using FluentResults;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.FO.Domain.Enums.LedgerJournals;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace MyApp.Functions;

public class JournalOrchestrator
{
    [Function(nameof(CreateJournalOrchestration))]
    public async Task<Result<string>> CreateJournalOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // 1. Create the journal header
        var header = new LedgerJournalHeader
        {
            DataAreaId = "USMF",
            JournalName = "GenJrn",
            Description = "Orchestrated journal - March 2026"
        };

        Result<LedgerJournalHeader> headerResult = await context.CallActivityAsync<Result<LedgerJournalHeader>>(
            nameof(JournalActivities.CreateJournalHeader), header);
        // Result: Result<LedgerJournalHeader> — serialised/deserialised via ResultJsonConverter

        // 2. Check for failure before proceeding
        if (headerResult.IsFailed)
        {
            return Result.Fail<string>(headerResult.Errors);
        }

        string journalBatchNumber = headerResult.Value.JournalBatchNumber!;

        // 3. Fan out — create multiple lines in parallel
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

        // 4. Fan in — await all line creation tasks
        Result<LedgerJournalLine>[] lineResults = await Task.WhenAll(lineTasks);

        // 5. Check for any line failures
        var failures = lineResults.Where(r => r.IsFailed).ToList();
        if (failures.Any())
        {
            var allErrors = failures.SelectMany(f => f.Errors).ToList();
            return Result.Fail<string>(allErrors);
        }

        return Result.Ok(journalBatchNumber);
    }
}
```

## Define the HTTP Starter

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;

namespace MyApp.Functions;

public class JournalStarter
{
    [Function(nameof(StartJournalOrchestration))]
    public async Task<HttpResponseData> StartJournalOrchestration(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(JournalOrchestrator.CreateJournalOrchestration));
        // Result: string — unique orchestration instance ID

        return client.CreateCheckStatusResponse(req, instanceId);
        // Result: HttpResponseData — 202 Accepted with status query URLs
    }
}
```

## Handle Errors in Orchestrations

Errors flow through `Result<T>` rather than exceptions. Check `IsFailed` after each activity call to decide whether to continue or abort:

```csharp
Result<LedgerJournalHeader> headerResult = await context.CallActivityAsync<Result<LedgerJournalHeader>>(
    nameof(JournalActivities.CreateJournalHeader), header);

if (headerResult.IsFailed)
{
    // Log the error context and return early
    return Result.Fail<string>(headerResult.Errors);
}

// Safe to proceed — header was created
string batchNumber = headerResult.Value.JournalBatchNumber!;
```

For fan-out scenarios, collect all failures and return them together:

```csharp
Result<LedgerJournalLine>[] results = await Task.WhenAll(lineTasks);

var failures = results.Where(r => r.IsFailed).ToList();
if (failures.Any())
{
    // Aggregate all errors from all failed lines
    return Result.Fail<string>(failures.SelectMany(f => f.Errors));
}
```

## When Things Go Wrong

**Missing `ResultJsonConverter`** — if the converters are not registered, `Result<T>` deserialises as an empty object after orchestration replay. The `IsSuccess` / `IsFailed` properties may return unexpected values and `Value` will be null:

```csharp
// Without converters registered:
Result<LedgerJournalHeader> headerResult = await context.CallActivityAsync<Result<LedgerJournalHeader>>(...);

// headerResult.Value is null even though the activity succeeded
// headerResult.IsSuccess may be true but Value is lost
```

**Activity function throws an exception** — if an activity throws instead of returning `Result.Fail`, the Durable Functions runtime wraps it in a `TaskFailedException`. Prefer returning `Result.Fail` from activities to keep error handling consistent:

```csharp
// Prefer this:
return Result.Fail<LedgerJournalHeader>(new IntegrationError("Code", "Message", ErrorType.Failure));

// Over this (loses structured error information):
throw new InvalidOperationException("Something went wrong");
```

## See Also

- [[Set-Up-an-Azure-Functions-Host]] — host configuration with JSON converters
- [[Create-a-Ledger-Journal]] — the commands used in orchestration activities
- [[Send-Your-First-Command]] — MediatR pipeline basics
