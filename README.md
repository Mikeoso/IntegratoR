# IntegratoR

.NET framework for building integrations with Microsoft Dynamics 365 Finance & Operations via OData on Azure Functions.

[![Build & Validate](https://github.com/Mikeoso/IntegratoR/actions/workflows/build.yml/badge.svg)](https://github.com/Mikeoso/IntegratoR/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

## Quick Start

Install the hosting package and register all services with a single call:

```bash
dotnet add package IntegratoR.Hosting
```

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddIntegratoR(context.Configuration, builder =>
        {
            builder.AddConsumerHandlers(Assembly.GetExecutingAssembly());
        });
    })
    .Build();
```

Add your D365 connection settings:

```json
{
  "ODataSettings": {
    "Url": "https://your-environment.operations.dynamics.com/data",
    "ClientId": "<app-registration-id>",
    "ClientSecret": "<client-secret>",
    "TenantId": "<tenant-id>",
    "Resource": "https://your-environment.operations.dynamics.com"
  }
}
```

See [Configure OData](https://github.com/Mikeoso/IntegratoR/wiki/Configure-OData) for all available settings (timeouts, retries, circuit breaker, local metadata).

Send a command to create a journal header:

```csharp
// injected via constructor: IMediator mediator

var header = new LedgerJournalHeader
{
    DataAreaId = "ps",
    JournalName = "GenJrn",
    Description = "Monthly accruals"
};

Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(header),
    cancellationToken).ConfigureAwait(false);

if (result.IsSuccess)
{
    string batchNumber = result.Value.JournalBatchNumber!;
}
```

Every operation returns a `Result<T>` -- no exceptions for business logic errors. The framework handles OAuth authentication, Polly retry policies, request validation, and structured logging automatically.

## Packages

| Package | Purpose |
|---------|---------|
| **IntegratoR.Hosting** | Unified DI registration -- install this one for the full framework |
| **IntegratoR.Abstractions** | Core interfaces, CQRS contracts, `Result` pattern, base entities |
| **IntegratoR.Application** | MediatR pipeline behaviours, generic handlers, OAuth, caching |
| **IntegratoR.OData** | Generic OData client, resilience policies, field serialization control |
| **IntegratoR.OData.FO** | D365 F&O entities, feature-specific handlers, financial dimensions |
| **IntegratoR.TestKit** | Custom `Result` assertions, fakes, test entity builders |

For most projects, add only `IntegratoR.Hosting` -- it pulls in all other packages as transitive dependencies. Add `IntegratoR.TestKit` to your test projects.

## What It Does

**Commands and queries** via MediatR with generic handlers for CRUD operations (`CreateCommand<T>`, `UpdateCommand<T>`, `DeleteCommand<T>`) and batch variants. Define your entity class, send a command -- the framework handles serialization, HTTP, and error mapping.

**Automatic resilience** with Polly policies: exponential backoff retries on transient failures (408, 429, 5xx) and a circuit breaker that stops requests when D365 is unresponsive. All configurable via `ODataSettings`.

**Pipeline behaviours** that run on every request in order: logging (structured, with timing), validation (FluentValidation), and caching (opt-in for queries via `ICacheableQuery`).

**Entity serialization control** with `[ODataField(IgnoreOnCreate = true)]` to exclude server-generated fields from POST payloads. The OData service reads these at runtime and builds the correct payload automatically.

**Financial dimension support** with `FinancialDimensionBuilder` to construct delimited dimension strings in the segment order D365 F&O expects.

## Documentation

Full documentation is in the [wiki](https://github.com/Mikeoso/IntegratoR/wiki):

- [Getting Started](https://github.com/Mikeoso/IntegratoR/wiki/Getting-Started) -- install, configure, send your first command
- [Define Entities](https://github.com/Mikeoso/IntegratoR/wiki/Define-Entities) -- map C# classes to D365 data entities
- [Send Commands](https://github.com/Mikeoso/IntegratoR/wiki/Send-Commands) -- create, update, delete, batch operations
- [Run Queries](https://github.com/Mikeoso/IntegratoR/wiki/Run-Queries) -- retrieve by key or filter, with caching
- [Handle Errors](https://github.com/Mikeoso/IntegratoR/wiki/Handle-Errors) -- `Result<T>`, `IntegrationError`, pattern matching
- [Configure OData](https://github.com/Mikeoso/IntegratoR/wiki/Configure-OData) -- auth, timeouts, retry and circuit breaker
- [Work with Dimensions](https://github.com/Mikeoso/IntegratoR/wiki/Work-with-Dimensions) -- build financial dimension strings
- [Write Tests](https://github.com/Mikeoso/IntegratoR/wiki/Write-Tests) -- TestKit assertions, fakes, builders

## Architecture

```
IntegratoR.Hosting (composition root)
  -> Application    -> Abstractions (core)
  -> OData          -> Abstractions
  -> OData.FO       -> OData -> Abstractions
```

Dependencies point inward. Business logic in Abstractions has no external dependencies. The OData and Application layers depend only on Abstractions. Hosting wires everything together.

## Development

```bash
dotnet build                  # Build the solution
dotnet test                   # Run all tests
dotnet format                 # Format code (.editorconfig enforced)
```

### Prerequisites

- .NET 10 SDK
- A D365 F&O environment (for integration testing)

### Project Structure

```
src/
  IntegratoR.Abstractions/    Core interfaces, CQRS contracts, error types
  IntegratoR.Application/     Pipeline behaviours, generic handlers, auth
  IntegratoR.OData/           OData client, Polly policies, field attributes
  IntegratoR.OData.FO/        D365 F&O entities, handlers, dimensions
  IntegratoR.Hosting/         Unified DI registration
tests/
  IntegratoR.TestKit/         Shared test infrastructure
  IntegratoR.*.Tests/         Unit tests per layer
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on submitting issues and pull requests.

## License

[MIT](LICENSE) -- Daniel Dieckmann
