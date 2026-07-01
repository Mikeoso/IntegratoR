# Voice reference + rejected patterns

Full detail behind the `SKILL.md` voice section, plus the record of documentation patterns observed in
other .NET libraries that were **deliberately rejected** for IntegratoR (the "exclude invalid
extractions" outcome of the doc-research effort). Read this before importing a pattern you saw elsewhere.

## Voice rules (full)

1. **Second person, imperative.** Start instructions with a verb: "Send the command", "Register the validator", "Check `result.IsSuccess`". Never third-person stand-ins ("the user", "the developer", "one", "the consumer") in instructional prose.
2. **Active voice, actor first.** "`AddIntegratoR` wires the converters", "The `ValidationBehaviour` rejects the request". Passive only when the actor is genuinely unknown ("The request is retried on HTTP 429"). Applies to section titles too.
3. **Lead with the answer, then context.** State the key fact/result in the first sentence; say what a concept *is* before why it matters. Cut "In this section we will…", "Note that…", "It is worth mentioning…".
4. **Lean and scannable.** 1–3 sentences of context, then code. Short declarative sentences, 2–3 sentence paragraphs, bullets and tables over dense blocks.
5. **Present tense for behaviour.** "The method returns a failed `Result`", not "will return". Drop routine "please"; avoid "at this time" and exclamation marks in body copy.
6. **Colleague tone.** Contractions and everyday words (it's, you'll, don't) are fine — never at the expense of precision. If it reads like an academic paper ("One might observe…"), rewrite it.
7. **British spelling in all prose.** Behaviour, serialisation, initialise, cancelled, categorise, licence (noun). Never Behavior/serialization/customize, even when quoting a general concept. Code identifiers keep their real casing.
8. **One term per concept,** IntegratoR's exact names: `Result<T>`, pipeline behaviour, composite key, command, query, handler, entity, `IntegrationError`, `ErrorType`. Never swap synonyms (handler/processor/consumer; command/message/request) for variety.
9. **State limitations bluntly, up front, one sentence** — no hedging. Add a single memorable guardrail line where a real anti-pattern exists ("Never write raw OData filter strings — use typed LINQ").
10. **Failure path in the same matter-of-fact register as success.** Show the failed `Result<T>` (`result.IsSuccess`, `result.GetError()`), the validation rejection (`Validation.Error`), or the HTTP 403. State the never-throw model plainly — business failures return a `Result`; they don't throw. Not alarmist, just factual.
11. **`result.GetError()`** consistently; never `result.Errors.FirstOrDefault()...`.
12. **Hub prose is pure navigation** — one positioning sentence + 1–2 sentences per link, zero code, no teaching.
13. **Humour essentially never.** Any rare light aside serves a technical point and stays out of titles, sidebar, hub tables, callout labels, and code. Calibrate well below Polly's chaos-monkey register — closer to its reference tone.
14. **Explain WHY only when non-obvious,** and keep it to one inline sentence on how-tos; push longer rationale to the concept page or an ADR.
15. **Realistic D365 domain terms** — `LedgerJournalHeader`, `DataAreaId "USMF"`; never foo/bar/contrived.
16. **Never ship unfinished prose** ("To be continued…", placeholder TODO sections) and never defer authoritative guidance to an external blog — omit the section or ship a tight finished subset.

## Banned words and tics

**Condescending qualifiers** (grep and delete): simply, just, easy, easily, trivial, trivially, obviously, of course, clearly. Replace with a concrete count — "it takes two config keys", "it is five lines".

**Marketing / superlatives:** powerful, seamless, seamlessly, blazing-fast, robust, enterprise-grade, elegant, world-class, lightweight (as praise), friendly, pleasant to use. Replace with a verifiable specific — what it does, its limits, its trade-offs.

**Filler adverbs:** quickly, effortlessly, effectively, basically, essentially — when they add no measurable meaning.

**Formatting tics:** ALL-CAPS emphatic directives (use a `> [!WARNING]` callout + a plain sentence); jokey code comments ("YAY! Do the thing"); AKA-slogan reference columns; emoji in tables/headings; the "royal we" for a solo-maintained framework (state recommendations as direct imperatives: "Prefer OAuth for…"); italics-on-first-use ceremony; deliberately varied sentence openings for their own sake; American spelling.

**Content that does not belong:** sponsor/donation appeals, licence-key prompts, funding asks (those live in `FUNDING.yml`/`CONTRIBUTING`); a "Why another library?" persuasion section (a one-sentence factual positioning line suffices — the reposition-the-framing follow-up is parked).

## Rejected patterns (seen in other .NET libraries, excluded for IntegratoR)

Each was observed in a real .NET library's docs and consciously left out because it is library-specific,
outdated, an anti-pattern, or fights IntegratoR's code-first / lean / British-spelling / single-rolling-wiki
house style.

| # | Rejected pattern (and where it was seen) | Why excluded |
|---|---|---|
| 1 | Mermaid state + happy/unhappy sequence diagrams on every feature page (Polly) | Ceremony for config/CRUD flows; reserve one diagram only for genuinely stateful, non-obvious order (token refresh, pipeline order). |
| 2 | Bespoke docs-site toolchain — DocFX, Sphinx/Read the Docs, Docusaurus, Mintlify, Jekyll/GitHub Pages mirror | Doesn't apply to the GitHub-wiki output; adds hosting overhead + split-brain drift for a solo maintainer. |
| 3 | Per-release versioned doc snapshots with a version selector (NodaTime, FluentValidation, EF Core) | Disproportionate for a single-rolling wiki at v2.0.0; use inline "(since vX.Y)" + a per-MAJOR migration page. |
| 4 | One long single-page README/Home with anchor-only nav (Dapper, Refit, FluentResults, MediatR) | Conflicts with one-page-per-concept; buries a multi-layer surface. Keep the multi-page wiki. |
| 5 | Outsource tutorials/examples to third-party sites or "read the tests/GitHub search" (Dapper→learndapper, AutoMapper→tests, MediatR) | Cedes accuracy control and drifts. The wiki owns first-party runnable examples backed by SampleFunction/TestKit. |
| 6 | Fan the tutorial into IDE/runtime variants (xUnit cmdline/VS/VS Code/Rider × netcore/netfx; Hangfire ASP.NET vs Core) | IntegratoR targets one stack (.NET 10 / Functions isolated worker); keep one canonical path. |
| 7 | Tabbed toolchain variants on one page (`# [.NET CLI]` / `# [Visual Studio]`) | A Microsoft Learn widget only; GitHub wikis have no native tab control. |
| 8 | Marketing landing sections ("Why another library?", superlatives, sponsor/donate blocks, AKA-slogan columns, emoji, humour-forward headings) | Fights the lean, precise, British-spelling voice. Home stays a pure routing hub. |
| 9 | Formal STS/LTS support-lifecycle / EOL-date statements (EF Core "supported until <date>") | Cannot be honoured on GitVersion continuous-delivery; worse than silence. |
| 10 | Component-first changelog with independently-versioned packages (xUnit, Serilog per-repo CHANGES) | IntegratoR publishes all `IntegratoR.*` in lockstep on one GitVersion number; one linear release list is correct. |
| 11 | Massive live-badged community catalogue pages (Serilog's 120+ sink table) | No such plugin ecosystem; a short curated list or a NuGet-tag link suffices. |
| 12 | Retired-page redirect stubs pointing to the site root (xUnit `redirect_url: /`) or hollow README-redirect stubs (FluentResults) | Lose reader context and create split-brain. Redirect to the specific replacement page. |
| 13 | "To be continued…" placeholders / depth deferred to an external blog (Hangfire) | Violates the ban on stale/aspirational docs. |
| 14 | Blend how-to + reference in one subsection with no generated reference (AutoMapper, FluentResults, Refit) | Conflicts with single-mode-per-page and the XML-doc reference investment. |
| 15 | Homepage-as-live-dashboard pulling releases/NuGet via JavaScript (xUnit `nuget-packages.js`) | A GitHub wiki runs no page JS; shields.io badges + a Releases link achieve the value. |
| 16 | A full-text Search page as a nav item (DocFX/Sphinx) | GitHub wikis already provide search. |
| 17 | Console-output screenshots as proof-of-success (Serilog, Hangfire) | Images rot, aren't searchable/diffable/CI-verifiable; use a copyable `Result<T>`/output text block. |
| 18 | Placeholder tokens (`<your-endpoint-here>`, TODO, `// configure as needed`) in the runnable critical path | Permitted only inside a config block, with a note on where the value comes from. |
| 19 | Rich Microsoft Learn front-matter (`ms.date`, `ms.topic`, `ms.author`, `feedback_system`, canonical URL) | No renderer on a GitHub wiki; keep only a plain "> Last verified" line + CODEOWNERS. |
| 20 | Snippet-injection toolchains wired to CI (MarkdownSnippets `#region`, DocFX `[!code]`, `docfx --warningsAsErrors`) | No build step on a plain wiki. The transferable slice — source examples from SampleFunction/tests — is kept. |
| 21 | A rigid full per-strategy template (About→Usage→Defaults→Telemetry→Diagrams→Resources→Anti-patterns) copied wholesale (Polly) | Over-structured; keep only the individual transferable ideas (code-first usage, options table, DON'T/DO, See Also). |
| 22 | Present multiple API styles neutrally with no recommendation | Causes choice paralysis; always bold a default + a "Choose X when" list. |
| 23 | Scatter design rationale / rejected alternatives across how-to/reference prose, or narrate implementation line-by-line as "depth" | Rationale goes to `docs/adr`; internals narration rots on the next refactor. |
| 24 | Hand-curated in-wiki changelog with `[NEW]/[FIX]/[BREAKING]` tags and `@contributor` credits (NSubstitute, Dapper) | Duplicates the auto-generated Releases/CHANGELOG and drifts. |
| 25 | Adopt the four literal Diataxis buckets as visible sidebar groups | A no-benefit big-bang rename; keep the shipped Get Started / Use Cases / Reference structure. |
| 26 | Match wiki prose to BCL third-person `<summary>` wording / ban second person | Those govern C# XML docs (owned by `csharp-documentation`), not wiki prose, which deliberately uses second person. |
| 27 | "Royal we" / "we recommend" voice, ALL-CAPS directives, jokey comments, italics-on-first-use, American spelling, "just works" reassurance | All conflict with the lean, imperative, British-spelling house voice. |
| 28 | `Microsoft.CodeAnalysis.PublicApiAnalyzers` / `ApiCompatBaseline.txt` embedded as the wiki "what changed" reference | A code/CI concern owned by `api-compatibility.md`, not an authorable wiki page. |
| 29 | Browser "attach image" CDN uploads for diagrams | Un-versioned, un-diffable, impermanent; commit images under `/images` with root-relative paths. |

## Kept from the research (the transferable wins)

For completeness — the patterns that survived reconciliation and are encoded in `SKILL.md`: Diataxis
mode-separation; code-first recipes; a curated routing Home; hand-authored `_Sidebar`/`_Footer`; a shallow
2-level tree; one-goal pages with verb/noun titles; mandatory `## See Also`; side-by-side API styles with a
bolded default; DON'T/DO pairs; explicit pipeline-order documentation; deferred conceptual depth; ADRs for
rationale; realistic bundled-entity examples; a runnable in-repo sample as the escape hatch; same-PR doc
coupling; freshness stamps; inline version tags; a curated version table + migration guide (not a
changelog); living Known-Limitations; rationed GitHub alert callouts; the hybrid settings page (prose +
options table); and incremental IA refactoring.
