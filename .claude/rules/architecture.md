# Architecture

Clean Architecture with dependencies pointing inward:

```
SampleFunction (host/composition root)
  -> Application    -> Abstractions (core)
  -> OData          -> Abstractions
  -> OData.FO       -> OData -> Abstractions
  -> RELion         -> Abstractions
```

## Layers

| Layer | Purpose | DI Entry Point |
|-------|---------|----------------|
| **Abstractions** | Domain interfaces (`IService<T>`, `ICommand`, `IQuery`), base entities (`BaseEntity<TKey>`), CQRS contracts, `Result` pattern (`IntegrationError`, `ErrorType`) | Core — no DI |
| **Application** | MediatR pipeline behaviours, generic command/query handlers, `OAuthAuthenticator`, cache services | `services.AddApplicationServices()` |
| **OData** | Generic OData client, `ODataService<T>`, auth handler, Polly policies, `ODataFieldAttribute` | `services.AddODataClient(configuration)` |
| **OData.FO** | D365 F&O entities, dimension queries, feature-specific commands/handlers | `services.AddODataClientFOProxy(configuration)` |
| **RELion** | RELion API integration (auth handler, service, entities, query handlers) | `services.AddRelionClient(configuration)` |
| **Hosting** | `IntegratoRBuilder`, composition root helpers | `services.AddIntegratoR(configuration)` |
| **TestKit** | Shared test infrastructure: custom `Result` assertions, test entity builders, fakes | Test helper — no DI |

## Key Patterns

- **CQRS via MediatR**: Commands and queries are `record` types implementing `ICommand<TResponse>` or `IQuery<TResponse>`.
- **Generic commands**: `CreateCommand<TEntity>`, `UpdateCommand<TEntity>`, `DeleteCommand<TEntity>` reusable with any `IEntity`.
- **Batch commands**: `CreateBatchCommand<TEntity>`, `UpdateBatchCommand<TEntity>`, `DeleteBatchCommand<TEntity>` for bulk operations.
- **Entity extensibility**: F&O entities inherit from `BaseEntity<TKey>` and must implement `GetCompositeKey()` (D365 uses composite keys like `DataAreaId` + business key).
- **`ODataFieldAttribute`**: Controls property serialisation — `IgnoreOnCreate`, `IgnoreOnUpdate` for server-generated or read-only fields.
- **Pipeline order**: Logging -> Validation -> Caching -> Handler (registration order matters in `AddApplicationServices()`).
- **Each layer has its own `ApplicationDependencyInjection`** class with extension methods for DI setup.

See `odata-conventions.md` for ODataSettings structure and entity patterns.
