---
vault_id: "integrator"
vault_name: "IntegratoR"
designation: "D365 F&O Integration Framework"
tech_stack:
  languages:
    - "C# (.NET 10)"
  frameworks:
    - "Azure Functions v4 (Isolated Worker)"
    - "Durable Task Extensions 1.7.1"
    - "MediatR 12.5.0"
    - "ASP.NET Core (DI/Configuration)"
  libraries:
    - "FluentResults 4.0.0"
    - "FluentValidation 12.0.0"
    - "Polly 8.5.0"
    - "Simple.OData.Client 6.0.1"
    - "MSAL (Microsoft.Identity.Client) 4.76.0"
    - "Newtonsoft.Json 13.0.3"
    - "StackExchange.Redis 2.8.16"
  databases:
    - "Redis (distributed caching)"
  infrastructure:
    - "Azure Functions"
    - "Azure Key Vault"
    - "Azure Application Insights"
    - "Azure Blob Storage"
  testing: []
  tooling:
    - "GitVersion (ContinuousDelivery)"
team:
  size: 1
  roles:
    - "Solo Developer"
created: "2026-02-18"
updated: "2026-02-18"
---

## Vault Summary

A .NET 10 integration framework providing a foundation for Dynamics 365 Finance & Operations integration with external systems. Uses Clean Architecture, CQRS (MediatR), FluentResults, and Azure Functions (Durable Tasks) to orchestrate data flows via OData. RELion is one of the supported target products, with the architecture designed to support additional integration targets.

## Architecture Overview

Clean Architecture with inward-only dependencies:

- **IntegratoR.Abstractions** — Domain interfaces, entities, CQRS contracts, Result types (innermost layer)
- **IntegratoR.Application** — Use cases, pipeline behaviours, cross-cutting concerns
- **IntegratoR.OData** — Generic OData client, HTTP, authentication, resilience
- **IntegratoR.OData.FO** — D365 Finance & Operations entity models and handlers
- **IntegratoR.RELion** — RELion OData integration and entity models
- **IntegratoR.SampleFunction** — Azure Functions host, composition root, orchestrators, activities

Key patterns:
- CQRS via MediatR with generic handlers (CreateCommandHandler<TEntity>, GetByKeyQueryHandler<TEntity>)
- Result pattern via FluentResults (no exceptions for flow control)
- Composite keys via GetCompositeKey() for D365 F&O multi-field keys
- Durable Functions for orchestration with fan-out/fan-in
- Dual JSON serializers: System.Text.Json for entities, Newtonsoft.Json for Durable Functions

## Conventions

- **British spelling** — `Behaviour` not `Behavior` throughout the codebase (intentional, never "correct")
- **FluentResults over exceptions** — All operations return `Result<T>` or `Result`; exceptions only for truly unexpected failures
- **Generic CQRS handlers** — Reduce boilerplate; any entity implementing `IEntity` works with generic handlers
- **Composite keys** — Every entity must implement `GetCompositeKey()` for generic key-based operations
- **Dual JSON serializers** — System.Text.Json (`[JsonPropertyName]`) for entity models; Newtonsoft.Json (`[JsonProperty]`) for Durable Functions serialization. Do not unify them.
- **GitVersion ContinuousDelivery** — Never manually edit `<Version>` in `.csproj` files
- **Branch naming** — `feature/<area>/<description>`, `fix/<area>/<description>`, `chore/<description>`
- **Commit messages** — Imperative mood, under 72 characters, atomic commits
- **CancellationToken propagation** — Through every async call chain
- **ConfigureAwait(false)** — In all library code (non-UI contexts)
- **No blocking on async** — No `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` in application code
- **Retry only transient, idempotent failures** — Exponential backoff with jitter, paired with circuit breaker
- **Cache only successful results** — Never cache errors or partial failures; explicit expiration always
- **Secrets in Key Vault / env vars** — Never hardcode; never log secrets
- **Validate at system boundaries** — Fail fast on invalid input
