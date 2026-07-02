# Architecture Decision Records

This directory holds **Architecture Decision Records (ADRs)** — short documents that capture a
significant, non-obvious design decision, the context that forced it, and its consequences.

## Why ADRs exist here

IntegratoR is a framework: some of its composition-root and infrastructure code exists because of an
external constraint (a library limitation, a D365 wire quirk, an upstream fork) that is **not visible
from the code itself**. That rationale is important, but a six-line inline `//` comment is the wrong
home for it — it bloats the method, drifts stale, and repeats across call sites.

The rule (see the `csharp-documentation` skill):

- **Short, non-obvious WHY** → a terse inline `//` comment at the point it matters.
- **Rationale a *consumer* needs** → the public member's XML `<remarks>`.
- **A significant *internal* design decision with real context** → an ADR here, with a one-line
  pointer in the code: `// Why …: see docs/adr/0001-....md`.

## Format (MADR-lite)

One file per decision, named `NNNN-kebab-title.md` (zero-padded, monotonically increasing). Copy
[`0000-template.md`](0000-template.md). Keep it to one page. Sections:

- **Status** — Accepted | Superseded by ADR-NNNN | Deprecated.
- **Context** — the forces at play; the constraint that made the obvious approach fail.
- **Decision** — what we chose, in the active voice ("We close the open generics in a single scan…").
- **Consequences** — the trade-offs, and what a future maintainer must not naively "simplify".

ADRs are **append-only history**: don't rewrite an accepted ADR to reflect a new decision — add a new
one and mark the old `Superseded by ADR-NNNN`.

## Index

| ADR | Title | Status |
|----|----|----|
| [0001](0001-generic-handler-and-validator-registration.md) | Cross-assembly generic handler & validator registration | Accepted |
| [0002](0002-durable-functions-result-converter-wiring.md) | Durable Functions `Result<T>` converter wiring | Accepted |
| [0003](0003-odata-expression-translator-fork.md) | Forked OData expression translator for `[JsonPropertyName]` | Accepted |
| [0004](0004-configurable-chunked-odata-batch.md) | Configurable, chunked OData `$batch` writes | Accepted |
