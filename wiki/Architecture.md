# Architecture

```
+------------------------------------------------------------------+
|                        SampleFunction                            |
|                   (Host / Composition Root)                      |
+----------+----------+-----------+----------+---------------------+
           |          |           |          |
           v          v           v          v
+----------+--+  +----+-----+  +-+--------+  +----------+
| Application |  |   OData  |  | OData.FO |  |  RELion  |
|             |  |          |  |          |  |          |
| MediatR     |  | OData    |  | F&O      |  | RELion   |
| Behaviours  |  | Client   |  | Entities |  | API      |
| Auth, Cache |  | Polly    |  | Builders |  | Service  |
+------+------+  +----+-----+  +----+-----+  +----+-----+
       |              |             |              |
       v              v             v              v
+------------------------------------------------------------------+
|                        Abstractions                              |
|                         (Core)                                   |
|                                                                  |
|  IService<T>, ICommand, IQuery, BaseEntity<TKey>,               |
|  IntegrationError, ErrorType, Result pattern                     |
+------------------------------------------------------------------+
```

## Dependency Flow

```
SampleFunction -----> Application -----> Abstractions
       |                                      ^
       +------------> OData -----------------+
       |                 ^                    |
       +------------> OData.FO --------------+
       |                                      |
       +------------> RELion ----------------+
```

Dependencies point inward. Abstractions depends on nothing (except FluentResults). The host depends on everything as the composition root.

## Projects

| Project | Layer | Purpose | DI Entry Point |
|---------|-------|---------|----------------|
| `IntegratoR.Abstractions` | Core | Domain interfaces, base entities, CQRS contracts, Result pattern | No DI (pure contracts) |
| `IntegratoR.Application` | Application | MediatR pipeline behaviours, generic handlers, auth, cache | `services.AddApplicationServices()` |
| `IntegratoR.OData` | Infrastructure | Generic OData client, Polly policies, auth handler | `services.AddODataClient(configuration)` |
| `IntegratoR.OData.FO` | Infrastructure | D365 F&O entities, dimension builders, F&O-specific handlers | `services.AddODataClientFOProxy(configuration)` |
| `IntegratoR.RELion` | Infrastructure | RELion API client, entities, query handlers | `services.AddRelionClient(configuration)` |
| `SampleFunction` | Host | Azure Functions triggers, DI composition root | N/A (entry point) |
| `IntegratoR.TestKit` | Test Support | Fakes, custom assertions, test builders | N/A (test helper) |

## Clean Architecture Mapping

| Clean Architecture Ring | IntegratoR Layer | Contents |
|------------------------|------------------|----------|
| **Entities** (innermost) | Abstractions | `BaseEntity<TKey>`, `IEntity`, domain enums |
| **Use Cases** | Application | Commands, queries, handlers, pipeline behaviours |
| **Interface Adapters** | OData, OData.FO, RELion | Service implementations, HTTP clients, data mapping |
| **Frameworks** (outermost) | SampleFunction | Azure Functions host, DI container setup |
