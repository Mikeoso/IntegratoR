# Architecture Overview

IntegratoR uses Clean Architecture with CQRS. Dependencies point inward toward the core Abstractions layer, keeping business logic independent of infrastructure concerns.

> **Prerequisites:** [[Getting-Started]], [[Register-Services-in-Your-Host]]

## Understand the Registration Order

Each layer provides its own `ApplicationDependencyInjection` class. Register in the host:

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        // 1. Application layer (MediatR pipeline + core services)
        services.AddApplicationServices();

        // 2. OData infrastructure
        services.AddODataClient(context.Configuration);

        // 3. F&O-specific services (depends on OData)
        services.AddODataClientFOProxy(context.Configuration);

        // 4. RELion infrastructure
        services.AddRelionClient(context.Configuration);
    })
    .Build();
```

## What Just Happened

Each registration call wires up a specific layer of the framework:

| Project | Layer | Purpose | DI Entry Point |
|---------|-------|---------|----------------|
| `IntegratoR.Abstractions` | Core | Domain interfaces, base entities, CQRS contracts, Result pattern | No DI (pure contracts) |
| `IntegratoR.Application` | Application | MediatR pipeline behaviours, generic handlers, auth, cache | `services.AddApplicationServices()` |
| `IntegratoR.OData` | Infrastructure | Generic OData client, Polly policies, auth handler | `services.AddODataClient(configuration)` |
| `IntegratoR.OData.FO` | Infrastructure | D365 F&O entities, dimension builders, F&O-specific handlers | `services.AddODataClientFOProxy(configuration)` |
| `IntegratoR.RELion` | Infrastructure | RELion API client, entities, query handlers | `services.AddRelionClient(configuration)` |
| `SampleFunction` | Host | Azure Functions triggers, DI composition root | N/A (entry point) |
| `IntegratoR.TestKit` | Test Support | Fakes, custom assertions, test builders | N/A (test helper) |

## Layer Diagram

```
+------------------------------------------------------------------+
|                        SampleFunction                            |
|                   (Host / Composition Root)                      |
|                                                                  |
|  Configures DI, wires all layers, hosts Azure Function triggers  |
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

## Trace the Dependency Flow

```
SampleFunction -----> Application -----> Abstractions
       |                                      ^
       +------------> OData -----------------+
       |                 ^                    |
       +------------> OData.FO --------------+
       |                                      |
       +------------> RELion ----------------+
```

Rules:
1. **Abstractions** depends on nothing (except FluentResults)
2. **Application** depends only on Abstractions
3. **OData** depends only on Abstractions
4. **OData.FO** depends on OData and Abstractions
5. **RELion** depends only on Abstractions
6. **SampleFunction** (host) depends on everything -- it is the composition root

## Clean Architecture Mapping

| Clean Architecture Ring | IntegratoR Layer | Contents |
|------------------------|------------------|----------|
| **Entities** (innermost) | Abstractions | `BaseEntity<TKey>`, `IEntity`, domain enums |
| **Use Cases** | Application | Commands, queries, handlers, pipeline behaviours |
| **Interface Adapters** | OData, OData.FO, RELion | Service implementations, HTTP clients, data mapping |
| **Frameworks** (outermost) | SampleFunction | Azure Functions host, DI container setup |

## Understand the Design Decisions

**Why IService\<T\> instead of IRepository\<T\>?**
The name `IService` was chosen because the implementation wraps an external API call rather than a traditional database. The pattern is functionally equivalent to a repository.

**Why generic commands?**
`CreateCommand<TEntity>`, `UpdateCommand<TEntity>`, and `DeleteCommand<TEntity>` eliminate boilerplate. A single generic handler serves all entity types through the same `IService<T>` contract.

**Why Result\<T\> instead of exceptions?**
Business errors (validation failures, not-found, API errors) are expected outcomes, not exceptional situations. `Result<T>` from FluentResults makes error handling explicit and composable.

**Why separate OData and OData.FO?**
`OData` is a generic, reusable OData client layer. `OData.FO` contains D365 F&O-specific entities and business logic. This separation allows the OData layer to be reused for other OData services.

## See Also

- [[Pipeline-Order]] — how requests flow through the MediatR pipeline
- [[Authentication-Modes]] — OAuth vs ApiKey architecture
- [[API-Reference]] — detailed API documentation for each layer
