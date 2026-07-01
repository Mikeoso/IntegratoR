# IntegratoR

A .NET 10 framework for building Microsoft Dynamics 365 Finance & Operations integrations on Azure Functions — it handles authentication, serialisation, resilience, batching, validation, and error handling through a CQRS pipeline, so you write the business logic and send a command.

## Documentation Map

### Get Started
| Guide | What it covers |
|---|---|
| [Getting Started](Getting-Started) | Install, wire `AddIntegratoR`, and send your first command end to end |
| [Configure OData](Configure-OData) | The full `ODataSettings` tree — connection, timeout, authentication, resilience — and fail-fast validation |
| [Define Entities](Define-Entities) | `BaseEntity` + `GetCompositeKey()`, `[ODataField]`, `[JsonPropertyName]`, and extending a built-in entity |
| [Send Commands](Send-Commands) | Create / Update / Delete singletons and batches, composite-key writes, and custom commands |
| [Run Queries](Run-Queries) | `GetByKeyQuery`, `GetByFilterQuery`, and typed LINQ-to-OData with `$orderby` |

### Use Cases
| Guide | What it covers |
|---|---|
| [Handle Errors](Handle-Errors) | The `Result<T>` never-throw model, `IntegrationError`, `ErrorType`, and `Match` |
| [Configure Resilience](Configure-Resilience) | Polly retry and circuit-breaker tuning, and what gets retried |
| [Add Validation](Add-Validation) | FluentValidation in the MediatR pipeline, and how generic validators are wired |
| [Cache Query Results](Cache-Query-Results) | `ICacheableQuery<T>` — in-memory by default, distributed as an opt-in |
| [Work with Dimensions](Work-with-Dimensions) | `GetDimensionOrdersQuery`, `FinancialDimensionBuilder`, and `FinancialDimensionReader` |
| [Run Smoke Tests](Run-Smoke-Tests) | Built-in HTTP triggers that verify a full CRUD flow against a sandbox |
| [Extend the Pipeline](Extend-the-Pipeline) | Custom commands, MediatR behaviours, and validators |
| [Test with TestKit](Test-with-TestKit) | Result assertions, fakes, and entity builders for your own tests |

### Reference
| Page | What it covers |
|---|---|
| [Understand the Architecture](Understand-the-Architecture) | Layering, the CQRS pipeline order, the dual-serialiser contract, and the composite-key write bypass |
| [Authentication Modes](Authentication-Modes) | OAuth 2.0 vs API Key (APIM), and Key Vault |
| [Set Up Azure Functions Host](Set-Up-Azure-Functions-Host) | A production `Program.cs`: Key Vault, Application Insights, and the JSON wiring |
| [Known Limitations](Known-Limitations) | What is genuinely limited today, tracked honestly |
| [Troubleshoot Common Issues](Troubleshoot-Common-Issues) | Real errors from sandbox runs and how to resolve them |
| [Release Notes and Versioning](Release-Notes-and-Versioning) | The versioning policy and per-MAJOR migration guides |

## See Also
- [Source on GitHub](https://github.com/Mikeoso/IntegratoR)
- [NuGet packages](https://www.nuget.org/packages?q=IntegratoR)
- [CLAUDE.md](https://github.com/Mikeoso/IntegratoR/blob/main/CLAUDE.md) — repository conventions for contributors
