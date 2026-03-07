# IntegratoR

.NET framework for building enterprise integration solutions targeting **Microsoft Dynamics 365 Finance & Operations** (D365 F&O) on Azure Functions. Uses Clean Architecture, CQRS with MediatR, FluentResults for error handling, and FluentValidation.

## Commands

```bash
dotnet build                                    # Build entire solution
dotnet test                                     # Run all tests
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"  # Run single test
dotnet test tests/IntegratoR.OData.Tests        # Run tests for one project
dotnet format                                   # Format code
dotnet format --verify-no-changes               # Check formatting (CI uses this)
```

## Architecture

Clean Architecture with dependencies pointing inward:

```
SampleFunction (host/composition root)
  -> Application    -> Abstractions (core)
  -> OData          -> Abstractions
  -> OData.FO       -> OData -> Abstractions
  -> RELion         -> Abstractions
```

| Layer | Purpose | DI Entry Point |
|-------|---------|----------------|
| **Abstractions** | Domain interfaces (`IService<T>`, `ICommand`, `IQuery`), base entities (`BaseEntity<TKey>`), CQRS contracts, `Result` pattern (`IntegrationError`, `ErrorType`) | Core — no DI |
| **Application** | MediatR pipeline behaviours, generic command/query handlers, `OAuthAuthenticator`, cache services | `services.AddApplicationServices()` |
| **OData** | Generic OData client via PanoramicData.OData.Client, `ODataService<T>`, auth handler, Polly policies, `ODataFieldAttribute` | `services.AddODataClient(configuration)` |
| **OData.FO** | D365 F&O entities (LedgerJournalHeader/Line, Dimensions), feature-specific commands/handlers/queries | `services.AddFOServices()` |
| **RELion** | RELion API integration with auth handler, service, entities, query handlers | `services.AddRelionClient(configuration)` |
| **TestKit** | Shared test infrastructure: custom `Result` assertions, test entity builders, fakes | Test helper — no DI |

## Key Patterns

- **CQRS via MediatR**: Commands and queries are `record` types implementing `ICommand<TResponse>` or `IQuery<TResponse>`.
- **Generic commands**: `CreateCommand<TEntity>`, `UpdateCommand<TEntity>`, `DeleteCommand<TEntity>` reusable with any `IEntity`. Handlers are also generic.
- **Batch commands**: `CreateBatchCommand<TEntity>`, `UpdateBatchCommand<TEntity>`, `DeleteBatchCommand<TEntity>` for bulk operations.
- **Entity extensibility**: F&O entities inherit from `BaseEntity<TKey>` and must implement `GetCompositeKey()` (D365 uses composite keys like `DataAreaId` + business key).
- **`ODataFieldAttribute`**: Controls property serialization — `IgnoreOnCreate`, `IgnoreOnUpdate` for server-generated or read-only fields.
- **Pipeline order**: Logging -> Validation -> Caching -> Handler (registration order matters in `AddApplicationServices()`).
- **Each layer has its own `ApplicationDependencyInjection`** class with extension methods for DI setup.
