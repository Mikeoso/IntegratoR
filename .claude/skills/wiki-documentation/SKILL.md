---
name: wiki-documentation
description: >-
  Use when writing, editing, reviewing, restructuring, or deploying IntegratoR
  GitHub WIKI pages (the in-repo /wiki folder synced to <repo>.wiki.git) —
  tutorial, how-to/recipe, reference, concept/architecture, troubleshooting,
  release-notes, and hub prose. Enforces the code-first, one-Diataxis-mode-per-page,
  lean British-spelling house voice; the three-group IA (Get Started / Use Cases /
  Reference); Title-Case-hyphenated filenames + [Text](Slug) links; per-page
  "Last verified against vX" freshness stamps; rationed GitHub alert callouts;
  show-the-failure-path; and docs-in-lockstep-with-source grounding. Do NOT use for
  C# XML doc comments (see csharp-documentation) or README / CHANGELOG / ADRs.
---

# IntegratoR Wiki Documentation

The GitHub wiki is IntegratoR's **conceptual, how-to, and contract-policy** surface — the code-first
guide a consumer reads to build a D365 F&O integration. It is not the API reference. Ownership is
split, and the split is load-bearing:

| Content | Owner | Not here |
|---|---|---|
| Member-level API reference (signatures, exact `Result<T>` failure shapes, parameter contracts) | Shipped **XML docs** (`GenerateDocumentationFile`, IntelliSense) — `csharp-documentation` skill | Never paste a method signature into wiki prose; deep-link to source instead |
| Non-obvious design rationale / rejected alternatives | **ADRs** under `docs/adr/` (MADR-lite) | Never scatter rationale across how-to/reference prose |
| Exhaustive change log | **CHANGELOG.md** + auto-generated GitHub Releases | The wiki links to them; it keeps only a curated version table + migration guide |
| **Conceptual, how-to, tutorial, reference-policy, troubleshooting prose** | **This skill → the wiki** | — |

The `/docs` (alias `/documentation`) command is the entry point that invokes this skill.

## The 10-second contract

Every page obeys all of these, no exceptions:

1. **Code-first.** Lead a how-to with the smallest working, copy-paste-runnable D365 example after ≤ 1–3 sentences of context. Theory comes *after* the code or is linked out.
2. **One Diataxis mode per page.** Tutorial / how-to / reference / concept — never mixed. Link across modes; never inline them.
3. **Show the failure path.** Every recipe that returns `Result<T>` shows the `IsFailed` / `result.GetError()` branch and the concrete D365 rejection, in the same calm register as success.
4. **Lean.** If a line is not a code block or a bullet, question whether it belongs. No walls of text.
5. **British spelling** in all prose (Behaviour, serialisation, initialise). Code identifiers keep their real casing.
6. **In lockstep with source.** Every claim and code block is verified against entity/handler source, the test suite, or a dated live run — **never** against another wiki page. Stale/aspirational docs are banned.
7. **`## See Also`** ends every page (2–4 internal `[Text](Slug)` links). No orphans.
8. **Freshness stamp.** Every reference/concept page carries `> Last verified against vX.Y.Z` directly under the H1.

## Before you write: ground it

Wiki examples have no compiler behind them, so grounding is the only freshness guarantee.

- **Read the source first.** Before any D365 entity example, open the entity and honour its `[ODataField(IgnoreOnCreate/IgnoreOnUpdate)]` and `[JsonPropertyName]` attributes — a snippet that ignores them is rejected live by D365 with **HTTP 403**. Use the current non-generic `BaseEntity` + `GetCompositeKey()`, **never** the `[Obsolete]` `BaseEntity<TKey>`.
- **The current ground-truth brief** lives at `.claude/plans/wiki-ground-truth.md` (capability map, exact signatures, shipped-entity `[ODataField]` matrix, v2.0.x facts, drift list, domain knowledge to preserve). Re-verify against source if it may be stale.
- **Prefer CI-exercised examples.** Mirror flows from `IntegratoR.SampleFunction` and the `TestKit`/xUnit tests so a signature change breaks the build, not just the reader's trust. Never delegate examples to "read the tests" or a third-party site.
- **Only the public entry point is `AddIntegratoR`.** `AddApplicationServices` / `AddODataClient` / `AddODataClientFOProxy` are internal composition steps — never cite them as consumer API.

## Conventions (GitHub-wiki mechanics)

- **Filenames:** Title-Case-with-hyphens so the slug equals the human title — `Send-Commands.md` → "Send Commands", slug `Send-Commands`. Never use `\ / : * ? " < > |` in a title (illegal on the Windows clone).
- **Internal links:** ONE syntax everywhere — Markdown slug links `[Send Commands](Send-Commands)`. **Never** `[[Page-Name]]` MediaWiki links, never hard-coded `https://github.com/.../wiki/...` URLs. Reserve full `[text](https://…)` links for genuinely external references. *(This corrects the stale "kebab-case + `[[Page-Name]]`" phrasing — follow the shipped convention.)*
- **Three special files, exactly:** `Home.md` (routing hub landing page), `_Sidebar.md` (site-wide nav), `_Footer.md` (persistent links: Source, NuGet, Releases). Do not bake footer links into the sidebar tail.
- **Images:** commit under `/images`, reference root-relative `![](/images/layer-diagram.png)`; PNG/JPEG/GIF only. Never the browser "attach image" upload (opaque CDN URL). Never a console screenshot as proof-of-success — use a copyable `Result<T>`/output text block.
- **Editing:** keep the wiki "Restrict editing to collaborators only". Author in `/wiki`, review in the same PR as the code, sync to `.wiki.git` on merge (see the deploy section).

## Information architecture

Keep the shipped **three visible sidebar groups, in this fixed order** — they already encode Diataxis
correctly for a ~15–25 page wiki. Diataxis informs *where content goes* (defer depth, never mix modes);
it does **not** relabel the sidebar into Tutorials/How-to/Reference/Explanation. New work slots a page
into a group; it does not re-architect the taxonomy.

- **Get Started** — the onboarding path + core setup, read top to bottom: `Getting-Started` (the single tutorial), `Configure-OData`, `Define-Entities`, `Send-Commands`, `Run-Queries`.
- **Use Cases** — independent how-to recipes: `Handle-Errors`, `Configure-Resilience`, `Add-Validation`, `Cache-Query-Results`, `Work-with-Dimensions`, `Run-Smoke-Tests`, `Extend-the-Pipeline`, `Test-with-TestKit`.
- **Reference** — concept/contract/operational: `Understand-the-Architecture` (the concept page), `Authentication-Modes`, `Set-Up-Azure-Functions-Host`, `Known-Limitations` (living), `Troubleshoot-Common-Issues`, `Release-Notes-and-Versioning`.

One grouping level only — absorb depth into in-page H2/H3, never child pages. `Home.md` is a pure routing
hub (positioning sentence + Documentation Map table + See Also, **zero code**). Update `_Sidebar.md` and the
`Home.md` map in the **same change** as any page add/rename/split. Refactor the IA incrementally — one page
at a time — never block on a full re-architecture.

## Page types — pick one mode

| Page type | Mode | Titling | When |
|---|---|---|---|
| Getting Started | Tutorial | — | Exactly one page; a single linear install → configure → first call → verify path, no branches |
| How-To / Recipe (Use Cases) | How-to | **Verb** + goal | One reader goal ("Send Commands", "Add Validation") |
| Reference | Reference | **Noun** phrase | Settings/modes/contract lookup ("Authentication Modes"); not member signatures |
| Concept / Architecture | Explanation | Noun phrase | Cross-cutting model invisible in one file ("Understand the Architecture") |
| Hub / Index | — | — | `Home.md`, `_Sidebar.md` — organise without explaining, zero code |
| Troubleshooting | How-to | — | Symptom-keyed real errors and fixes |
| Release Notes | Reference | — | Version table + per-MAJOR migration; not the exhaustive changelog |

**Full copy-ready skeletons for each type → `references/page-templates.md`.**

## Voice

Write like a precise colleague explaining at a whiteboard — direct, lean, factual.

- **Second person, imperative:** "Send the command", "Check `result.IsSuccess`". Never "the user / the developer / one".
- **Active voice, actor first:** "`AddIntegratoR` wires the converters", not "the converters are wired".
- **Lead with the answer,** then context. Cut preamble ("In this section we will…", "Note that…").
- **One term per concept,** IntegratoR's exact names: `Result<T>`, pipeline behaviour, composite key, command, query, handler, entity, `IntegrationError`, `ErrorType`. Never swap synonyms for variety.
- **State footguns bluntly, up front, one sentence** — no hedging. "A single `IgnoreOnUpdate` field in the payload makes D365 reject the whole PATCH with HTTP 403."
- **Use `result.GetError()`** consistently; never the verbose LINQ alternative.
- **Humour essentially never;** any rare aside serves a technical point and never lands in a title, sidebar, callout, or code.
- **Realistic D365 domain terms** — `LedgerJournalHeader`, `DataAreaId "USMF"`; never foo/bar.

**Banned words (grep and delete):** simply, just, easy/easily, trivial, obviously, of course, clearly; powerful, seamless, blazing-fast, robust, enterprise-grade, elegant, lightweight (as praise); quickly, effortlessly, basically, essentially (as filler). **Banned tics:** ALL-CAPS directives (use a callout), jokey code comments, emoji in headings/tables, the "royal we", American spelling, "To be continued…" placeholders. **Full list + rationale → `references/voice-and-exclusions.md`.**

## Callouts — ration them

GitHub renders `> [!NOTE]`, `> [!WARNING]`, `> [!CAUTION]` natively. Use them **only** for genuine
load-bearing footguns; a page with a callout on every paragraph has none.

- **WARNING** — MAJOR-breaking / security / data-loss: `ReasonPhrase` leakage; a single `IgnoreOnUpdate` field making D365 reject the whole PATCH (403).
- **CAUTION** — ordering / perf / bounds: pipeline order `Logging → Validation → Caching → Handler`; retry idempotent reads only; `RetryCount` 1–10.
- **NOTE** — behavioural surprise: composite-key PATCH returns 204 → `Result<T>.Value` is still non-null; dual-serialiser lockstep.

## Multiple valid styles → steer, don't shrug

When the framework offers more than one way (JSON config vs `ConfigureOData`; generic `CreateCommand<T>`
vs entity-specific/batch; `IMediator.Send` vs a direct `IService<T>` call; OAuth vs API Key; in-memory vs
distributed cache): show the **same** D365 scenario each way back-to-back, **bold the recommended default**
up front, and close with a `Choose X when… / Choose Y when…` list keyed to concrete capabilities. Add a
tight **DON'T / DO** pair (code + one consequence line) wherever a real footgun exists. Never present
competing styles neutrally with no recommendation.

## Freshness & governance

- **Same-PR coupling.** Update the affected page in the **same PR** as any change to a public signature, a config key (`ODataSettings`/`FOSettings`), an `[ODataField]` attribute, an entity's IgnoreOnCreate/Update set, or observable `Result<T>` behaviour. "Docs updated?" is a mandatory reviewer-checklist item.
- **Freshness stamp.** `> Last verified against vX.Y.Z` under the H1 of every reference/concept page; re-stamp on every edit. Behaviour only a live server can confirm (composite-key PATCH 204, HTTP 403 on a read-only field, orphan/self-clean) is stamped `Verified against live D365 (JFI) on YYYY-MM-DD`, citing the smoke run that proved it.
- **Version tags inline.** Mark version-sensitive behaviour at point-of-use — "(since v2.0.0)" — not only in the changelog. The wiki is a single rolling "latest" surface; no per-release doc trees, no version switcher.
- **Prune resolved limitations.** When a `Known-Limitations` gap closes, delete it from the live list and move the resolution into the version table / migration guide (e.g. composite-key writes are RESOLVED as of v2.0.0). Never leave a stale limitation or a placeholder.
- **Deprecation window.** Keep a deprecated type's/page's doc for ≥ 1 MINOR showing the replacement. When repositioning, leave a short "Moved to [Replacement](Replacement-Slug)" stub — never a Home redirect, never a dead link.

## Pre-publish checklist (grep-testable)

- [ ] First non-heading element of a how-to is a fenced, runnable code block within ~3 sentences.
- [ ] Every `Result<T>` recipe shows an `IsFailed` / `GetError()` branch.
- [ ] `grep '[[' ` → zero; every internal link is `[Text](Slug)`; no hard-coded wiki URLs.
- [ ] `grep 'BaseEntity<'` → zero; every entity example uses non-generic `BaseEntity`.
- [ ] `grep` banned words (simply/just/powerful/seamless/…) → zero.
- [ ] Every page ends with `## See Also` (2–4 links) and is reachable from `_Sidebar.md` + ≥ 1 other page.
- [ ] Every reference/concept page has a `> Last verified against` stamp.
- [ ] No pasted method signatures; API mentions deep-link to source.
- [ ] British spelling throughout prose.
- [ ] `_Sidebar.md` has exactly the three group headings in order; page count 15–25.

## Deploy `/wiki` → `.wiki.git`

The wiki repo is `https://github.com/Mikeoso/IntegratoR.wiki.git` (branch `master`). Sync from the in-repo
`/wiki` folder — never hand-edit in the web UI. See `references/page-templates.md` for the deploy recipe
(clone `.wiki.git`, mirror `/wiki/*.md` including `_Sidebar.md`/`_Footer.md`, `git rm` removed pages,
commit, push). A CI sync job (e.g. `Andrew-Chen-Wang/github-wiki-action`) is the eventual home; the
`.wiki.git` must be bootstrapped with one stub page via the UI before CI can push to it.

## What NOT to do

The 29 documentation patterns from other .NET libraries that were **considered and rejected** for
IntegratoR (bespoke docs-site toolchains, per-release doc trees, mega-README Home, outsourced examples,
marketing sections, screenshot-as-proof, …) are recorded with reasons in
**`references/voice-and-exclusions.md`** — read it before importing a pattern you saw elsewhere.
