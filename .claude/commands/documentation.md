Generate optimised documentation for IntegratoR.

$ARGUMENTS

If $ARGUMENTS is empty, generate a documentation plan for the entire codebase.
If $ARGUMENTS names a component, layer, or type (e.g., "OData", "CreateCommand", "pipeline behaviours"), generate documentation for that specific area.

---

## Documentation Context

### Standards

1. Always use one page per concept, command, or task — never combine topics on a single page.
2. Always create hub pages that organise without explaining — 1-2 sentences per link, zero code. Avoid jargon in link descriptions — don't say "via CQRS" or "MediatR pipeline" in hub text; use plain language the audience understands.
3. Always organise by what the user wants to do, not by what the library contains.
4. Always layer docs as: Setup → Getting Started → How-Tos → API Reference. Configuration/setup pages belong before or alongside Getting Started, not buried in Guides.
5. Always lead with a working code example before any explanation — 1-3 sentences of context max, then code.
6. Always use realistic D365 F&O domain examples — never foo/bar/contrived samples.
7. Always show what the code produces — expected Result object, HTTP response, or console output.
8. Always strip examples to the absolute minimum needed to demonstrate the concept.
9. Always order examples from simple to complex within each page.
10. Always show multiple API styles when the library offers them (MediatR command vs direct service call).
11. Always show what failure looks like — failed Result contents, validation rejection, API error response. Every page must include at least one failure code block, including Getting Started.
12. Always use action-verb headings that answer "What can I do?" — never passive/descriptive titles. Page titles: "Define Entities" not "Entities", "Send Commands" not "Commands", "Handle Errors" not "Error Handling", "Configure Settings" not "Configuration", "Test with the TestKit" not "Testing".
13. Always mirror type names in page names for predictable discovery. When a concept page covers multiple types, ensure each type is discoverable via search or cross-links.
14. Always use a consistent template per page type (see Templates below). Every template section is mandatory — do not omit "See Also", "Prerequisites", "When Things Go Wrong", or "What Just Happened".
15. Always document parameters with type, required/optional, default value, and validation constraints. Use a consistent table format with a "Required" column.
16. Always end every page with a `## See Also` section linking 2-4 related topics using `[[Page-Name]]` wiki-link syntax.
17. Always make every code example copy-paste-runnable — include usings, DI registration, configuration. On first use of `mediator` or `cancellationToken` on a page, add a comment showing how to obtain them (e.g., `// injected via constructor: IMediator mediator`).
18. Always explain CQRS by showing it working, never by defining the acronym. On the first page that introduces `mediator.Send()`, add a one-line callout: "Think of `mediator.Send()` as a pipeline that automatically logs, validates, and caches your operations."

### Constraints

1. Never write walls of text — if it can't be a code block or a bullet, it doesn't belong.
2. Never start with architecture/theory before showing concrete usage.
3. Never skip showing how to wire things up (DI, config, registration).
4. Never duplicate content across pages — link instead. If the same type (e.g., `ODataFieldAttribute`, `IService<T>`) appears on multiple pages, pick one canonical page and link from others.
5. Never assume users read pages sequentially — every page is a valid entry point.
6. Never require understanding the architecture to use the framework.
7. Never use passive/descriptive titles ("Advanced Features") — use action titles ("Batch Multiple Operations").
8. Never document implementation — show HOW TO USE, not HOW IT WORKS internally. Name the actor in explanations: "The `ODataService<T>` excludes properties marked..." not "Properties are excluded...".
9. Never use passive voice to describe framework behaviour — name the component responsible (e.g., "The `ValidationBehaviour` short-circuits the pipeline" not "The pipeline is short-circuited").
10. Never show code that would fail due to `[ODataField(IgnoreOnCreate/Update)]` stripping fields — always verify which fields survive serialisation before using an entity in examples.

### Landmines

1. Code examples go stale after API changes — treat examples as code that must compile and run. Before writing any example using a D365 entity, read the entity source to check which properties have `[ODataField(IgnoreOnCreate/Update = true)]` — examples that populate stripped fields silently fail.
2. Happy-path-only docs leave users stranded — always show the failure path.
3. Docs that live separately from code drift out of sync — co-locate with source, version together.
4. D365 quirks (composite keys, cross-company, batch limits) must be surfaced where they bite, not in a separate "gotchas" page. Specifically:
   - **DataAreaId**: Explain that it scopes every operation to a legal entity. Omitting it or providing a wrong value targets the wrong company's data.
   - **Cross-company queries**: Note that multi-company queries require `cross-company=true`.
   - **Batch limits**: Surface the ~5,000 operation limit and chunking pattern inline, with a bold warning about non-atomicity.
   - **Rate limiting**: D365 aggressively throttles OData (HTTP 429) — mention this where retry policies are configured.
5. Users who don't understand CQRS won't admit it — teach by showing working commands/queries, not definitions.
6. Users will copy-paste and modify — structure docs so copy-paste users succeed immediately. The `"null"` sentinel in `GetCompositeKey()` needs an inline comment explaining why it exists.
7. `GetLoggingContext()` is a mandatory implementation detail that confuses newcomers — simplify or defer it in first-encounter examples.

### Audience

**Who:** Internal team developers and external consumers building D365 F&O integrations on Azure Functions.

**What they've tried:** Simple.OData.Client (hit D365 limitations), Power Automate/Logic Apps (couldn't handle complex orchestration), D365 Data Management Framework (hit performance/flexibility walls).

**Afraid of:** Wasting time learning a framework that doesn't cover their D365 scenario. Breaking production integrations with silent failures or data corruption.

**Won't say out loud:**
- "I don't understand CQRS" — they'll quietly struggle with commands, queries, and pipeline behaviours.
- "D365 OData is painful" — they assume the framework handles composite keys, cross-company, and batch limits magically.
- "I'll just copy-paste" — they won't read architecture docs; they'll find the closest example and modify it.
- "I don't know what OData is" — they need one sentence of context before seeing JSON config blocks.

**Design for:** 5-minute time-to-first-success. Before/after comparisons that prove value. Failure paths shown alongside success paths.

**Value proposition (surface explicitly):** The Home or Getting Started page must demonstrate concrete advantages over alternatives. Show what IntegratoR handles that Simple.OData.Client does not: composite key URL construction, per-operation field exclusion (`ODataFieldAttribute`), built-in retry/circuit breaker for D365 throttling, and financial dimension string building.

---

## Page Templates

### Getting Started Page

```
# [Action-Verb Title]

[1-2 sentence context with link to prerequisites]

> **Prerequisites:** .NET 10+, Azure Functions Core Tools, D365 F&O environment with Azure AD app registration

## Install

[Package installation command]

## Configure

[DI registration code — copy-paste-runnable]

## First [Operation]

[Minimal working example with output, including `// injected via constructor` comments for mediator/cancellationToken]

## When It Fails

[Show the same operation failing — validation error, NotFound, or D365 error — with Result.IsFailed handling]

## What Just Happened

[2-3 bullet explanation of what the code did]

## See Also
- [[Related-Topic-1]]
- [[Related-Topic-2]]
```

### How-To Page

```
# [Action-Verb Title]

[1-2 sentence context]

> **Prerequisites:** [[Getting-Started]] completed, [any other requirements]

## [Step as Action Verb]

[Code example with output]

## [Next Step as Action Verb]

[Code example with output]

## When Things Go Wrong

[Failure example with Result.Fail output — use realistic D365 error scenarios]

## See Also
- [[Related-Topic-1]]
- [[Related-Topic-2]]
```

### API Reference Page

```
# [TypeName]

[1 sentence purpose]

## Usage

[Minimal working example with usings and `// injected via constructor` comments]

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
- [[Related-Type-1]]
- [[Related-Type-2]]
```

### Hub/Index Page

```
# [Topic Area]

[1-2 sentence overview — plain language, no jargon]

## [Category — ordered by learning sequence]
- **[[Page-Title]]** — [1 sentence description in plain language]
- **[[Page-Title]]** — [1 sentence description in plain language]

## [Category]
- **[[Page-Title]]** — [1 sentence description in plain language]
```

Hub page link ordering must follow the natural learning sequence (e.g., Entities → Commands → Queries → Error Handling → Validation → Batch Operations → Caching → Resilience → Configuration).

---

## Output Target

All documentation is written to the **GitHub wiki** of this repository. Each page becomes a wiki page.

- Use `gh api` or `git` commands to interact with the wiki repository.
- Wiki page filenames use kebab-case with `.md` extension (e.g., `Create-an-Entity.md`).
- Hub pages link to other wiki pages using `[[Page-Name]]` wiki-link syntax.
- The wiki Home page serves as the top-level hub.
- "See also" links use `[[Page-Name]]` syntax to link between wiki pages.

## Steps

1. If `$ARGUMENTS` is empty:
   - Read the solution structure: `dotnet sln list` or scan for .csproj files.
   - Read each project's public API surface (key types, commands, queries, services).
   - Generate a documentation plan: list of pages needed, organised as a hub structure, with page type (Getting Started, How-To, Reference, Hub) for each.
   - Map each planned page to a wiki page name (kebab-case).
   - Output the plan as a markdown document.

2. If `$ARGUMENTS` names a component:
   - Find the relevant source files using Glob/Grep for the named component.
   - Read the source code to understand the public API, parameters, usage patterns, and error scenarios.
   - **Read the entity source to check `[ODataField]` attributes before writing any example** — verify which fields survive create/update serialisation.
   - Read existing tests for the component to extract realistic usage examples.
   - Generate documentation following the appropriate template from above.
   - Include: working code examples, failure examples, DI setup, parameter table, and "See also" `[[wiki-links]]`.
   - Write the generated page(s) to the GitHub wiki.

3. After generating:
   - Verify all code examples reference real types and methods from the codebase.
   - **Verify no example populates fields that would be stripped by `[ODataField(IgnoreOnCreate/Update)]`** — this is the most common source of silently broken examples.
   - Verify all "See also" links reference wiki pages that exist or are planned.
   - Check that every page has at least one failure/error example.
   - Check that every page ends with a `## See Also` section.
   - Check that every How-To page has a `> **Prerequisites:**` block and a `## When Things Go Wrong` section.
   - Check that every page title uses an action verb, not a descriptive noun.
   - Check that `mediator` and `cancellationToken` have `// injected via constructor` comments on first use per page.
   - Use `result.GetError()` consistently — never `result.Errors.OfType<IntegrationError>().First()`.
