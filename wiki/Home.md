# IntegratoR

.NET framework for building enterprise integration solutions targeting Microsoft Dynamics 365 Finance & Operations on Azure Functions.

### Getting Started

- [[Getting-Started|Get Started]] — Install, configure, and send your first command
- [[Azure-Functions-Host|Set Up Azure Functions Host]] — Production-ready Azure Functions host setup

### Architecture

- [[Architecture|Understand the Architecture]] — Layer diagram, dependency flow, and project mapping

### Guides

- [[Entities|Define Entities]] — Model D365 entities with `BaseEntity<TKey>` and `ODataFieldAttribute`
- [[Commands|Send Commands]] — Create, update, and delete entities via CQRS
- [[Queries|Run Queries]] — Query by composite key or filter expression
- [[Batch-Operations|Run Batch Operations]] — Bulk create, update, and delete
- [[Error-Handling|Handle Errors]] — `Result<T>` pattern with `IntegrationError`
- [[Validation|Validate Input]] — FluentValidation in the MediatR pipeline
- [[Caching|Cache Results]] — Cache query results with `ICacheableQuery`
- [[Resilience|Configure Resilience]] — Retry policies and circuit breaker configuration
- [[Configuration|Configure Settings]] — OData, F&O, and RELion settings reference
- [[D365-FO-Journals|Work with D365 F&O Journals]] — Ledger journals and financial dimensions
- [[RELion|Integrate with RELion]] — RELion API integration

### Reference

- [[Extending-the-Pipeline|Extend the Pipeline]] — Custom commands and pipeline behaviours
- [[Durable-Functions|Use Durable Functions]] — Durable Functions orchestration patterns
- [[Testing|Write Tests]] — TestKit fakes and assertion helpers

## See Also

- [IntegratoR on NuGet](https://www.nuget.org/packages?q=IntegratoR) — published packages
- [[Getting-Started|Get Started]] — first steps for new users
