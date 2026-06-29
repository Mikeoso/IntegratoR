# Architecture

Clean Architecture with dependencies pointing inward:

```
SampleFunction (host/composition root)
  -> Application    -> Abstractions (core)
  -> OData          -> Abstractions
  -> OData.FO       -> OData -> Abstractions
```

## Layers

| Layer | Purpose | DI Entry Point |
|-------|---------|----------------|
| **Abstractions** | Domain interfaces (`IService<T>`, `ICommand`, `IQuery`), base entities (`BaseEntity<TKey>`), CQRS contracts, `Result` pattern (`IntegrationError`, `ErrorType`) | Core — no DI |
| **Application** | MediatR pipeline behaviours, generic command/query handlers, `OAuthAuthenticator`, cache services | `services.AddApplicationServices()` |
| **OData** | Generic OData client, `ODataService<T>`, auth handler, Polly policies, `ODataFieldAttribute` | `services.AddODataClient(configuration)` |
| **OData.FO** | D365 F&O entities, dimension queries, feature-specific commands/handlers | `services.AddODataClientFOProxy(configuration)` |
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

## Serialisation

The codebase uses **two JSON serialisers** and `Result<T>` needs custom converters in both. Both converter families delegate to a shared `ResultJsonShape` helper so the wire format stays in lockstep — never let them drift.

| Serialiser | Used by | Result converters | Wiring |
|---|---|---|---|
| **System.Text.Json** | Durable Functions isolated worker SDK (`JsonDataConverter`), `DistributedCacheService` | `IntegratoR.Abstractions/Common/Results/SystemText/ResultJsonConverter.cs` (factory + typed + non-generic) | **Auto** — `AddIntegratoR()` wires `DurableTaskWorkerOptions.DataConverter`; `DistributedCacheService` registers them in its own static `JsonSerializerOptions`. Consumers do nothing. |
| **Newtonsoft.Json** | HTTP trigger payloads (`JsonConvert.SerializeObject`/`DeserializeObject`), journal file parsing | `IntegratoR.Abstractions/Common/Results/ResultJsonConverter.cs` (3 classes: non-generic, generic factory, typed) | **Manual** — `JsonConvert.DefaultSettings = ...` block in `SampleFunction/Program.cs`. Process-global mutable state; do NOT auto-wire from `AddIntegratoR`. |

**Shared shape helper:** `IntegratoR.Abstractions/Common/Results/ResultJsonShape.cs` — internal class (with `InternalsVisibleTo IntegratoR.Abstractions.Tests`) holding property-name constants and the `IError ↔ (code, message, type)` projection/hydration. Both converter families call `Project` and `Hydrate` here.

**Lenient on non-`IntegrationError`:** `ResultJsonShape.Project` accepts any `IError` and falls back to `(Unknown, error.Message, Failure)` for non-`IntegrationError` types. This is intentional — the public converters are library API and external consumers may pass plain `Result.Fail("message")`. Do NOT tighten this without an explicit breaking-change plan.

**Adding a new STJ code path:** call `.AddResultConverters()` on your `JsonSerializerOptions` (idempotent — safe to call repeatedly).

**Adding a new Newtonsoft code path:** the global `JsonConvert.DefaultSettings` hook covers you automatically.

**Test the wiring, not just the converter:** unit tests on a converter in isolation don't catch wiring bugs. Use real `MemoryDistributedCache` (`DistributedCacheServiceResultRoundTripTests`) and real `JsonDataConverter` (`DurableTaskJsonDataConverterResultTests`) to prove the registered options reach the actual code path. Cross-serialiser compatibility is pinned by `tests/IntegratoR.Abstractions.Tests/Common/Results/CrossSerializerCompatibilityTests.cs`.

See `odata-conventions.md` for ODataSettings structure and entity patterns.
