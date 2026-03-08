# Architecture

IntegratoR follows Clean Architecture principles with dependencies pointing inward toward the core abstractions layer. This section covers the layer structure, request pipeline, and authentication patterns.

## Deep Dives

| Page | Description |
|------|-------------|
| [[Architecture-Overview]] | Layer diagram, dependency flow, and project-to-layer mapping |
| [[Pipeline-Order]] | MediatR pipeline: Logging, Validation, Caching, Handler |
| [[Authentication-Modes]] | OAuth vs ApiKey -- when to use each |

## Quick Reference

```
SampleFunction (host / composition root)
    |
    +-> Application        -> Abstractions (core)
    +-> OData              -> Abstractions
    +-> OData.FO           -> OData -> Abstractions
    +-> RELion             -> Abstractions
```

**Key principles:**
- Dependencies point inward -- outer layers depend on inner layers, never the reverse
- The Abstractions layer has zero external dependencies
- Each layer registers its own services via `ApplicationDependencyInjection` extension methods
- All service methods return `Result<T>` -- no exceptions for business flow control

## See Also

- [[Architecture-Overview]] -- detailed layer breakdown
- [[Pipeline-Order]] -- request pipeline deep dive
- [[Getting-Started]] -- framework introduction
