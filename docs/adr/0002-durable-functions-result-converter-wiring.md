# ADR-0002: Durable Functions `Result<T>` converter wiring

- **Status:** Accepted
- **Date:** 2026-07-01

## Context

Azure Durable Functions (isolated worker) serialises activity/orchestrator inputs and outputs with
its own `JsonDataConverter` over System.Text.Json. IntegratoR's `Result<T>`/`Result` need custom STJ
converters to round-trip through the task hub (see `architecture.md` — the dual-serialiser reality).
Consumers should not have to wire this by hand, but consumers who are **not** building Durable
Functions should pay nothing for it.

## Decision

`AddIntegratoR` registers the converters lazily via `services.Configure<DurableTaskWorkerOptions>`,
setting `options.DataConverter = new JsonDataConverter(DurableTaskJsonOptions)`, where
`DurableTaskJsonOptions` is a single `static readonly` `JsonSerializerOptions(Web).AddResultConverters()`.

- The `Configure` callback runs only if something resolves `DurableTaskWorkerOptions` — a consumer not
  using Durable Functions never triggers it, so the cost is zero at runtime.
- A single static options instance keeps STJ's per-instance converter-metadata cache warm for the
  host's lifetime (same rationale as `DistributedCacheService`'s shared options).

## Consequences

- Durable Functions consumers get `Result<T>` round-tripping automatically; non-Durable consumers pay
  no runtime cost (the `Microsoft.DurableTask.*` package references are unconditional but tiny and
  almost always already present in an Azure-Functions integration's dependency tree).
- **Do not** eagerly resolve `DurableTaskWorkerOptions` or replace `WorkerOptions.Serializer` — that
  would impose the converter on every consumer and defeat the lazy design.
- Keep `DurableTaskJsonOptions` a single static instance; constructing options per call throws away
  the converter cache.
