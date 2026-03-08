# IntegratoR

.NET framework for building enterprise integration solutions targeting Microsoft Dynamics 365 Finance & Operations on Azure Functions.

### Getting Started

- [[Getting-Started]] — Install, configure, and send your first command
- [[Azure-Functions-Host]] — Production-ready Azure Functions host setup

### Architecture

- [[Architecture]] — Layer diagram, dependency flow, and project mapping

### Guides

- [[Entities]] — Define entities with `BaseEntity<TKey>` and `ODataFieldAttribute`
- [[Commands]] — Create, update, and delete entities via CQRS
- [[Queries]] — Query by composite key or filter expression
- [[Batch-Operations]] — Bulk create, update, and delete
- [[Error-Handling]] — `Result<T>` pattern with `IntegrationError`
- [[Validation]] — FluentValidation in the MediatR pipeline
- [[Caching]] — Cache query results with `ICacheableQuery`
- [[Resilience]] — Retry policies and circuit breaker configuration
- [[Configuration]] — OData, F&O, and RELion settings reference
- [[D365-FO-Journals]] — Ledger journals and financial dimensions
- [[RELion]] — RELion API integration

### Reference

- [[Extending-the-Pipeline]] — Custom commands and pipeline behaviours
- [[Durable-Functions]] — Durable Functions orchestration patterns
- [[Testing]] — TestKit fakes and assertion helpers
