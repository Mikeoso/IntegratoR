# IntegratoR

A .NET 10 framework for building enterprise integrations with **Microsoft Dynamics 365 Finance & Operations** on Azure Functions. The framework handles authentication, serialisation, resilience, batching, validation, and error handling so the consumer focuses on business logic.

```csharp
// Inject IMediator, then:
LedgerJournalHeader header = new()
{
    DataAreaId = "USMF",
    JournalName = "GenJrn",
    Description = "Monthly accruals"
};

Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(header),
    cancellationToken).ConfigureAwait(false);
```

The same `mediator.Send(...)` call also runs validation, captures structured logs, retries transient HTTP failures with exponential backoff, short-circuits when the circuit breaker is open, and never throws an exception for business-level errors — every operation returns a `Result<T>` from FluentResults.

## Why IntegratoR

- **One composition call** — `services.AddIntegratoR(configuration)` wires the entire stack: MediatR pipeline behaviours, OData client, OAuth/APIM authentication, Polly resilience policies, and built-in D365 F&O handlers.
- **CQRS by default** — generic `CreateCommand<T>`, `UpdateCommand<T>`, `DeleteCommand<T>`, `GetByKeyQuery<T>`, `GetByFilterQuery<T>` work with any entity; custom commands only when the generic shape is insufficient.
- **D365 F&O composite-key aware** — entities inherit from `BaseEntity<TKey>` and implement `GetCompositeKey()`. The framework handles the composite-key URL construction for OData reads.
- **Type-safe filter translation** — LINQ expressions like `h => h.DataAreaId == "USMF"` translate to `$filter=dataAreaId eq 'USMF'`. The translator honours `[JsonPropertyName]` so PascalCase CLR properties map cleanly to camelCase D365 wire fields.
- **Result pattern throughout** — `Result<T>` and `IntegrationError` with `ErrorType` categorisation (Failure, Validation, NotFound, Conflict). Pattern-match with `result.Match(onSuccess, onFailure)` or check `result.IsSuccess` and `result.GetError()` directly.
- **Production-ready resilience** — Polly retry with exponential backoff and jitter on 408, 429, 5xx, plus a circuit breaker that protects the downstream when it is unhealthy.

## Documentation Map

### Get Started

| Guide | What it covers |
|---|---|
| [Getting Started](Getting-Started) | Install packages, configure services, send the first command end-to-end |
| [Configure OData](Configure-OData) | Full `ODataSettings` reference — connection, timeout, authentication, resilience |
| [Define Entities](Define-Entities) | `BaseEntity<TKey>`, composite keys, `[ODataField]`, `[JsonPropertyName]` |
| [Send Commands](Send-Commands) | Create / Update / Delete singletons and batches, custom commands |
| [Run Queries](Run-Queries) | `GetByKeyQuery`, `GetByFilterQuery`, LINQ-to-OData translation |

### Use Cases

| Guide | What it covers |
|---|---|
| [Handle Errors](Handle-Errors) | `Result<T>`, `IntegrationError`, `ErrorType`, `Match` pattern |
| [Configure Resilience](Configure-Resilience) | Polly retry tuning, circuit breaker tuning, what gets retried |
| [Add Validation](Add-Validation) | FluentValidation in the MediatR pipeline, validator registration |
| [Cache Query Results](Cache-Query-Results) | `ICacheableQuery<T>`, in-memory vs distributed cache |
| [Work with Dimensions](Work-with-Dimensions) | `FinancialDimensionBuilder` / `Reader`, `GetDimensionOrdersQuery` |
| [Run Smoke Tests](Run-Smoke-Tests) | Built-in HTTP triggers for live end-to-end verification against a sandbox |
| [Integrate with RELion](Integrate-with-RELion) | Optional RELion API module |
| [Extend the Pipeline](Extend-the-Pipeline) | Custom commands, custom MediatR behaviours, custom validators |
| [Test with TestKit](Test-with-TestKit) | Result assertions, fakes (`FakeCacheService`, `FakeHttpMessageHandler`), entity builders |

### Reference

| Page | What it covers |
|---|---|
| [Understand the Architecture](Understand-the-Architecture) | Layer diagram, dependency flow, project map |
| [Authentication Modes](Authentication-Modes) | OAuth 2.0 vs API Key (APIM), Key Vault integration |
| [Set Up Azure Functions Host](Set-Up-Azure-Functions-Host) | Production `Program.cs`, Key Vault, Application Insights |
| [Known Limitations](Known-Limitations) | Composite-key write paths, other parked items (transparent) |
| [Troubleshoot Common Issues](Troubleshoot-Common-Issues) | Real errors from sandbox runs and how to resolve them |
| [Release Notes and Versioning](Release-Notes-and-Versioning) | Semantic versioning, pre-release vs stable, migration tips |

## Installation

```bash
dotnet add package IntegratoR.Hosting
```

`IntegratoR.Hosting` pulls in `IntegratoR.Application`, `IntegratoR.OData`, `IntegratoR.OData.FO`, and `IntegratoR.Abstractions` as transitive dependencies. The optional `IntegratoR.RELion` package is installed separately. See [Getting Started](Getting-Started) for the full walkthrough.

## Repository Conventions

- **British spelling** is used intentionally (`Behaviour`, `serialise`) throughout the codebase.
- **`Result<T>`** is the only return shape for operations that can fail — exceptions are reserved for genuinely exceptional infrastructure failures.
- **`ConfigureAwait(false)`** is applied in every async call in library code.
- **Central Package Management** is used — package versions live in `Directory.Packages.props`, never in individual `.csproj` files.

## See Also

- [Source on GitHub](https://github.com/Mikeoso/IntegratoR)
- [NuGet packages](https://www.nuget.org/packages?q=IntegratoR)
- [Release Notes and Versioning](Release-Notes-and-Versioning)
- [CLAUDE.md](https://github.com/Mikeoso/IntegratoR/blob/main/CLAUDE.md) — repository conventions and CLI commands for contributors
