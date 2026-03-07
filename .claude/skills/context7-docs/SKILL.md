---
name: context7-docs
description: This skill should be used when the user asks to "look up docs for Polly", "find code examples for MediatR", "check FluentValidation setup", "verify library API usage", "how to configure Redis", or needs documentation or code snippets for any non-Microsoft third-party library.
version: 0.1.0
context: fork
---

# Context7 Documentation & Code Reference

Query up-to-date, version-specific documentation and code examples for any non-Microsoft library, framework, or package using the Context7 MCP server.

**Prerequisite:** Requires Context7 MCP Server (`@upstash/context7-mcp`).

## Scope

| Use This Skill | Use Microsoft Skills Instead |
|----------------|------------------------------|
| Third-party libraries (MediatR, Polly, FluentValidation) | Azure SDKs (`Azure.*`, `Microsoft.*`) |
| Open-source frameworks (React, Express, Django) | .NET BCL and runtime APIs |
| Language ecosystems (npm, PyPI, NuGet non-Microsoft) | Microsoft Graph, Office 365 APIs |
| Infrastructure tools (Docker, Terraform, Redis) | Azure services documentation |

When a query involves both Microsoft and non-Microsoft libraries, use **both** skills to get complete coverage.

## Tools

| Step | Tool | Purpose |
|------|------|---------|
| 1 | `mcp__context7__resolve-library-id` | Resolve a package name to a Context7 library ID |
| 2 | `mcp__context7__query-docs` | Query documentation and code examples using the resolved ID |

**Always resolve the library ID first.** The `query-docs` tool requires an exact Context7-compatible library ID (format: `/org/project`).

## Two-Step Workflow

### Step 1: Resolve Library ID

```
resolve-library-id(
  libraryName: "MediatR",
  query: "CQRS command handler pipeline behaviour"
)
```

**Selection criteria when multiple results appear:**
- Prioritise exact name matches
- Prefer higher Code Snippet counts (more practical examples)
- Prefer High/Medium reputation sources
- Higher Benchmark Score indicates better quality

### Step 2: Query Documentation

```
query-docs(
  libraryId: "/jbogard/mediatr",
  query: "How to create a pipeline behaviour for validation"
)
```

**Query tips for better results:**
- Be specific about the task: `"retry policy with exponential backoff"` not `"retry"`
- Include version when relevant: `"Polly v8 resilience pipeline"` not `"Polly retry"`
- Describe the intent: `"configure dependency injection for MediatR handlers"` not `"DI"`

## Common Use Cases

### Setup & Configuration

```
resolve-library-id(libraryName: "Polly", query: "configure resilience pipeline with retry and circuit breaker")
query-docs(libraryId: "/<id-from-step-1>", query: "configure resilience pipeline retry circuit breaker dependency injection")
```

### Code Examples & Patterns

```
resolve-library-id(libraryName: "FluentValidation", query: "create custom validator with async rules")
query-docs(libraryId: "/<id-from-step-1>", query: "custom validator async validation rules example")
```

### API Usage Verification

```
resolve-library-id(libraryName: "PanoramicData.OData.Client", query: "batch operations OData v4")
query-docs(libraryId: "/<id-from-step-1>", query: "batch create update operations OData v4 entities")
```

### Migration & Version Changes

```
resolve-library-id(libraryName: "Polly", query: "migrate from Polly v7 to v8")
query-docs(libraryId: "/<id-from-step-1>", query: "migration guide v7 to v8 breaking changes")
```

## Validation Workflow

Before generating code using third-party libraries, verify correctness:

1. **Resolve the library** — `resolve-library-id(libraryName: "[package]", query: "[specific need]")`
2. **Query for patterns** — `query-docs(libraryId: "/<id-from-step-1>", query: "[specific API or pattern]")`
3. **Cross-reference with project** — Compare retrieved patterns against existing codebase usage

For simple lookups, steps 1-2 suffice. For complex integrations or unfamiliar APIs, complete all three.

## When to Verify

Always verify when:
- Using a library API for the first time
- Upgrading to a new major version (breaking changes)
- Method or class name seems uncertain (possible hallucination)
- Configuration pattern seems non-standard
- Mixing patterns from different library versions

## Query Effectiveness

### Effective Queries

| Need | Query Style |
|------|-------------|
| Setup/install | `"getting started setup configuration"` |
| Specific method | `"[ClassName] [MethodName] parameters"` |
| Pattern/recipe | `"how to [task] with [library]"` |
| Configuration | `"configure [feature] options settings"` |
| Error resolution | `"[error message or type] troubleshooting"` |
| Version-specific | `"v[X] [feature] breaking changes"` |

### Limitations

- Maximum 3 calls to `resolve-library-id` per question
- Maximum 3 calls to `query-docs` per question
- Results may be summarised for large documentation sets
- Some niche or very new libraries may have limited coverage

When Context7 returns insufficient results, fall back to web search or project-local documentation.

## Additional Resources

### Reference Files

For detailed query patterns organised by library type and ecosystem:

- **`references/query-patterns.md`** — Ecosystem-specific patterns for NuGet, npm, PyPI, and infrastructure tools. Includes IntegratoR-relevant library examples.