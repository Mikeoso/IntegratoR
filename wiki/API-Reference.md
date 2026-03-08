# API Reference

Complete reference for all public types in the IntegratoR framework, organised by layer.

## Abstractions

Core interfaces, base types, and CQRS contracts that define the framework's domain model.

- **[[API-ICommand]]** -- Command interfaces for write operations (`ICommand<TResponse>`, `ICommand`)
- **[[API-IQuery]]** -- Query interface for read operations (`IQuery<TResponse>`)
- **[[API-BaseEntity]]** -- Abstract base class for domain entities (`BaseEntity<TKey>`, `IEntity`)
- **[[API-Generic-Commands]]** -- Pre-built CRUD commands (`CreateCommand<T>`, `UpdateCommand<T>`, `DeleteCommand<T>`, and batch variants)
- **[[API-Generic-Queries]]** -- Pre-built query types (`GetByKeyQuery<T>`, `GetByFilterQuery<T>`)
- **[[API-IntegrationError]]** -- Error model with typed codes (`IntegrationError`, `ErrorType`, `ResultExtensions`)
- **[[API-IService]]** -- Service interface for data access (`IService<TEntity>`)
- **[[API-ICacheableQuery]]** -- Cacheable query contract (`ICacheableQuery<TResponse>`)

## Application

Pipeline behaviours, dependency injection, and cross-cutting concerns.

- **[[API-Pipeline-Behaviours]]** -- MediatR pipeline behaviours (`LoggingBehaviour`, `ValidationBehaviour`, `CachingBehaviour`)
- **[[API-AddApplicationServices]]** -- DI registration entry point (`AddApplicationServices()`)

## OData

- **[[API-ODataService]]** -- Generic OData client implementation (`ODataService<T>`, `IODataService<T>`, `IODataBatchService<T>`)
- **[[API-ODataFieldAttribute]]** -- Property serialisation control for OData entities
- **[[API-ODataSettings]]** -- All OData connection and resilience configuration options
- **[[API-AddODataClient]]** -- OData layer DI registration

## OData.FO

- **[[API-LedgerJournalHeader]]** -- D365 F&O general journal header entity
- **[[API-LedgerJournalLine]]** -- D365 F&O general journal line entity
- **[[API-FinancialDimensionBuilder]]** -- Fluent builder for financial dimension strings
- **[[API-FOSettings]]** -- F&O-specific configuration (dimension formats, hierarchy types)

## RELion

- **[[API-RelionSettings]]** -- RELion connection and authentication configuration
- **[[API-RelionService]]** -- RELion API client for ledger mappings and journal lines

## TestKit

- **[[API-TestKit]]** -- Test fakes, builders, and custom assertions (`FakeCacheService`, `FakeHttpMessageHandler`, `TestEntityBuilder`, `ResultAssertions`)

## See Also

- [[Home]]
- [[Getting-Started]]
- [[How-To-Guides]]
