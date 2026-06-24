# IntegratoR

A .NET 10 framework for building enterprise integrations with **Microsoft Dynamics 365 Finance & Operations** on Azure Functions. The framework handles authentication, serialisation, resilience, batching, validation, and error handling so the consumer focuses on business logic.

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

## See Also

- [Source on GitHub](https://github.com/Mikeoso/IntegratoR)
- [NuGet packages](https://www.nuget.org/packages?q=IntegratoR)
- [Release Notes and Versioning](Release-Notes-and-Versioning)
- [CLAUDE.md](https://github.com/Mikeoso/IntegratoR/blob/main/CLAUDE.md) — repository conventions and CLI commands for contributors
