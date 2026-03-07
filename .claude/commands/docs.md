Generate optimised documentation for IntegratoR.

$ARGUMENTS

If $ARGUMENTS is empty, generate a documentation plan for the entire codebase.
If $ARGUMENTS names a component, layer, or type (e.g., "OData", "CreateCommand", "pipeline behaviours"), generate documentation for that specific area.

---

## Documentation Context

### Standards

1. Always use one page per concept, command, or task — never combine topics on a single page.
2. Always create hub pages that organise without explaining — 1-2 sentences per link, zero code.
3. Always organise by what the user wants to do, not by what the library contains.
4. Always layer docs as: Setup → Getting Started → How-Tos → API Reference.
5. Always lead with a working code example before any explanation — 1-3 sentences of context max, then code.
6. Always use realistic D365 F&O domain examples — never foo/bar/contrived samples.
7. Always show what the code produces — expected Result object, HTTP response, or console output.
8. Always strip examples to the absolute minimum needed to demonstrate the concept.
9. Always order examples from simple to complex within each page.
10. Always show multiple API styles when the library offers them (MediatR command vs direct service call).
11. Always show what failure looks like — failed Result contents, validation rejection, API error response.
12. Always use action-verb headings that answer "What can I do?" — never passive/descriptive titles.
13. Always mirror type names in page names for predictable discovery.
14. Always use a consistent template per page type (see Templates below).
15. Always document parameters with type, required/optional, default value, and validation constraints.
16. Always end every page with "See also" linking 2-4 related topics.
17. Always make every code example copy-paste-runnable — include usings, DI registration, configuration.
18. Always explain CQRS by showing it working, never by defining the acronym.

### Constraints

1. Never write walls of text — if it can't be a code block or a bullet, it doesn't belong.
2. Never start with architecture/theory before showing concrete usage.
3. Never skip showing how to wire things up (DI, config, registration).
4. Never duplicate content across pages — link instead.
5. Never assume users read pages sequentially — every page is a valid entry point.
6. Never require understanding the architecture to use the framework.
7. Never use passive/descriptive titles ("Advanced Features") — use action titles ("Batch Multiple Operations").
8. Always document usage, not implementation — show HOW TO USE, not HOW IT WORKS internally.

### Landmines

1. Code examples go stale after API changes — treat examples as code that must compile and run.
2. Happy-path-only docs leave users stranded — always show the failure path.
3. Docs that live separately from code drift out of sync — co-locate with source, version together.
4. D365 quirks (composite keys, cross-company, batch limits) must be surfaced where they bite, not in a separate "gotchas" page.
5. Users who don't understand CQRS won't admit it — teach by showing working commands/queries, not definitions.
6. Users will copy-paste and modify — structure docs so copy-paste users succeed immediately.

### Audience

**Who:** Internal team developers and external consumers building D365 F&O integrations on Azure Functions.

**What they've tried:** Simple.OData.Client (hit D365 limitations), Power Automate/Logic Apps (couldn't handle complex orchestration), D365 Data Management Framework (hit performance/flexibility walls).

**Afraid of:** Wasting time learning a framework that doesn't cover their D365 scenario. Breaking production integrations with silent failures or data corruption.

**Won't say out loud:**
- "I don't understand CQRS" — they'll quietly struggle with commands, queries, and pipeline behaviours.
- "D365 OData is painful" — they assume the framework handles composite keys, cross-company, and batch limits magically.
- "I'll just copy-paste" — they won't read architecture docs; they'll find the closest example and modify it.

**Design for:** 5-minute time-to-first-success. Before/after comparisons that prove value. Failure paths shown alongside success paths.

---

## Page Templates

### Getting Started Page

```
# [Action-Verb Title]

[1-2 sentence context with link to prerequisites]

## Install

[Package installation command]

## Configure

[DI registration code — copy-paste-runnable]

## First [Operation]

[Minimal working example with output]

## What Just Happened

[2-3 bullet explanation of what the code did]

## See Also
- [Related topic 1]
- [Related topic 2]
```

### How-To Page

```
# [Action-Verb Title]

[1-2 sentence context]

> **Prerequisites:** [link to setup page]

## [Step as Action Verb]

[Code example with output]

## [Next Step as Action Verb]

[Code example with output]

## When Things Go Wrong

[Failure example with Result.Fail output]

## See Also
- [Related topic 1]
- [Related topic 2]
```

### API Reference Page

```
# [TypeName]

[1 sentence purpose]

## Usage

[Minimal working example]

## Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|

## Examples

### Basic
[Simple example with output]

### With [Variation]
[More complex example with output]

### Error Handling
[Failure example with Result output]

## See Also
- [Related type 1]
- [Related type 2]
```

### Hub/Index Page

```
# [Topic Area]

[1-2 sentence overview]

## [Category]
- **[Page Title]** — [1 sentence description]
- **[Page Title]** — [1 sentence description]

## [Category]
- **[Page Title]** — [1 sentence description]
```

---

## Steps

1. If `$ARGUMENTS` is empty:
   - Read the solution structure: `dotnet sln list` or scan for .csproj files.
   - Read each project's public API surface (key types, commands, queries, services).
   - Generate a documentation plan: list of pages needed, organised as a hub structure, with page type (Getting Started, How-To, Reference, Hub) for each.
   - Output the plan as a markdown document.

2. If `$ARGUMENTS` names a component:
   - Find the relevant source files using Glob/Grep for the named component.
   - Read the source code to understand the public API, parameters, usage patterns, and error scenarios.
   - Read existing tests for the component to extract realistic usage examples.
   - Generate documentation following the appropriate template from above.
   - Include: working code examples, failure examples, DI setup, parameter table, and "See also" links.

3. After generating:
   - Verify all code examples reference real types and methods from the codebase.
   - Verify all "See also" links reference pages that exist or are planned.
   - Check that every page has at least one failure/error example.
