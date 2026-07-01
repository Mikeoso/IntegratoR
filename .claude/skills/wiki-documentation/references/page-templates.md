# Wiki page skeletons

Copy-ready skeletons for each page type. Pick exactly one mode per page (see `SKILL.md`). Replace the
`vX.Y.Z` stamp with the current stable version when you instantiate a page. Every skeleton ends with
`## See Also`.

---

## Getting Started (the single tutorial)

**One page only.** A learning-oriented on-ramp that guarantees a first success. Everything branchy
(OAuth vs ApiKey, batch, resilience, composite-key nuance, custom entities) links out — never inlined.

```markdown
# Getting Started
> Last verified against vX.Y.Z

<1 sentence: what you'll have working by the end.>

## Prerequisites
<terse: .NET 10 SDK (preview); an isolated-worker Functions project (in-process NOT supported);
a D365 F&O environment + Azure AD app registration; an OAuth client secret OR an APIM subscription key.>

## 1. Install
`dotnet add package IntegratoR.Hosting`

## 2. Configure
<ODataSettings in local.settings.json (placeholders allowed here — note where each value comes from)
plus a single AddIntegratoR(configuration) line. That is the entire wiring step.>

## 3. Send your first command
<copy-paste-runnable: usings + host entry + a real LedgerJournalHeader (DataAreaId "USMF",
non-generic BaseEntity) sent via IMediator.>

## 4. Verify the result
<success: check result.IsSuccess, read result.Value.JournalBatchNumber.
Failure side-by-side: result.GetError() -> IntegrationError Code/Message. State the never-throw model.>

## What just happened (optional)
<numbered mental model AFTER the working code: Logging -> Validation -> Caching -> Handler,
OAuth token acquisition, Polly retry/circuit-breaker. No line-by-line internals.>

## Run the full sample
<link IntegratoR.SampleFunction + its smoke-test triggers as the clone-and-run escape hatch.>

## See Also
- [Configure OData](Configure-OData)
- [Define Entities](Define-Entities)
- [Send Commands](Send-Commands)
- [Set Up Azure Functions Host](Set-Up-Azure-Functions-Host)
```

---

## How-To / Recipe (Use Cases)

**Single goal, pure how-to.** A reader who already knows the framework wants one thing done.

```markdown
# <Action Verb + Goal>
> Last verified against vX.Y.Z

<1-3 sentences of context, no more.>

<SMALLEST working copy-paste-runnable code block achieving the goal — one D365 scenario end-to-end:
define -> register in DI -> send via IMediator -> inspect Result<T>.>

## Handle the failure path
<same scenario failing: result.IsFailed / result.GetError(), branch on IntegrationError.Type,
show the concrete D365 rejection (403 / validation / 401).>

## Choose between <X> and <Y>   (only if multiple valid styles exist)
<same example each way, then **bold default** + "Choose X when… / Choose Y when…" list.>

## DON'T / DO   (only where a real footgun exists)
<terse pairs: code + one consequence line.>

> [!WARNING] / [!CAUTION] / [!NOTE] <only for a genuine load-bearing trap>

<an observable result that proves it worked: the emitted $filter, a 204 -> Result<TEntity> round-trip,
a surfaced validation error.>

## See Also
- <2-4 links incl. the concept page for the WHY and the reference for the options>
```

---

## Reference (settings / modes / contract)

**Lookup content.** Settings matrices, modes, contract-level facts — NOT member-level API signatures
(those live in the XML docs).

```markdown
# <Noun Phrase>
> Last verified against vX.Y.Z

<1-2 sentences: what this configures or controls.>

<short runnable example for the common why/when.>

## Options
| Property | Type | Default | Purpose |
|---|---|---|---|
<exhaustive, scannable; group nested objects (Authentication, Resilience).>

> [!CAUTION] <ordering/perf or bounds trap — e.g. RetryCount 1-10 is documented, not enforced — only if real>

## See Also
- <2-4 links; deep-link API mentions to source/IntelliSense rather than pasting signatures>
```

---

## Concept / Architecture

**Cross-cutting explanation** invisible in any single file. One page; reserve for genuinely
cross-cutting ideas. A small model can instead be a one-paragraph preamble on the relevant how-to.

```markdown
# Understand <the Concept>
> Last verified against vX.Y.Z

<the mental model in a paragraph — what it is before why it matters.>

## <Durable invariant 1>   (e.g. dependency direction points inward)
## <Durable invariant 2>   (e.g. pipeline order Logging -> Validation -> Caching -> Handler + WHY + silent-failure symptom)
## <Durable invariant 3>   (e.g. Result<T> two-serialiser wire contract kept in lockstep)

<numbered list or ONE Mermaid diagram, only where order/state is load-bearing and non-obvious.
NO line-by-line implementation tour. Link significant decisions to docs/adr/NNNN. Link API mentions to source.>

## See Also
- <2-4 links to the how-tos that apply the concept + relevant ADRs>
```

---

## Hub / Index (`Home.md`, `_Sidebar.md`, `_Footer.md`)

**Organise without explaining. Zero code, zero deep content.**

```markdown
<!-- Home.md -->
# IntegratoR
<one positioning sentence: "A .NET 10 framework for building D365 F&O integrations on Azure Functions…">

## Documentation Map
### Get Started
| Guide | What it covers |   (one <=2-sentence line per link)
### Use Cases
| Guide | What it covers |
### Reference
| Page | What it covers |

## See Also
- [Source on GitHub](https://github.com/Mikeoso/IntegratoR)
- [NuGet packages](https://www.nuget.org/packages?q=IntegratoR)
- [Release Notes and Versioning](Release-Notes-and-Versioning)
```

```markdown
<!-- _Sidebar.md -->
**[IntegratoR](Home)**

**Get Started**
- [Getting Started](Getting-Started)
- …

**Use Cases**
- …

**Reference**
- …
```

```markdown
<!-- _Footer.md -->
[Source on GitHub](https://github.com/Mikeoso/IntegratoR) · [NuGet](https://www.nuget.org/packages?q=IntegratoR) · [Releases](https://github.com/Mikeoso/IntegratoR/releases)
```

---

## Troubleshooting

**Symptom-keyed.** Real errors readers hit and how to resolve them.

```markdown
# Troubleshoot Common Issues
> Last verified against vX.Y.Z (live-D365 items dated per run)

## <Symptom / the error message the reader sees>
<Cause in one sentence.> <Fix as an imperative + minimal code/config.>
<For server-observed behaviour: "Verified against live D365 (JFI) on YYYY-MM-DD".>

> [!WARNING] <only for a real trap, e.g. an IgnoreOnUpdate field -> whole-PATCH 403>

<repeat per symptom; keep entries scannable, symptom-first.>

## See Also
- [Handle Errors](Handle-Errors)
- [Known Limitations](Known-Limitations)
- [Configure Resilience](Configure-Resilience)
```

---

## Release Notes / Versioning

**Version table + human upgrade narrative.** NOT the exhaustive changelog (that is `CHANGELOG.md` +
GitHub Releases).

```markdown
# Release Notes and Versioning
> Last verified against vX.Y.Z

<1-2 sentences: GitVersion continuous-delivery, pre-release vs stable, deprecate-before-remove policy.
Link CHANGELOG.md + GitHub Releases for the full log.>

## Released Versions
| Version | Highlights |   (curated; each row links to the GitHub Release)

## Upgrading to vN (per MAJOR)
<blast radius up front: breaking or not.>
### <Breaking change 1>
<before/after code or config block> — <one-line "change this".>
<Cover the real v2.0.0 breaks: FindEntriesAsync orderBy param; batch IEnumerable -> IReadOnlyList;
GetDimensionOrdersQuery PascalCase rename; BaseEntity<TKey> deprecation.>

## See Also
- [Known Limitations](Known-Limitations)
- [Define Entities](Define-Entities)
- [GitHub Releases](https://github.com/Mikeoso/IntegratoR/releases)
```

---

## Deploy recipe: `/wiki` → `.wiki.git`

The GitHub wiki is a separate git repo. Sync from the in-repo `/wiki` folder; never hand-edit in the UI.
Bootstrap once by creating a stub Home page in the GitHub wiki UI (the `.wiki.git` does not exist until
the first page is created).

```powershell
# from a scratch dir, NOT inside the main working tree
git clone https://github.com/Mikeoso/IntegratoR.wiki.git wiki-publish
# mirror /wiki/*.md (incl. _Sidebar.md, _Footer.md, Home.md); exclude non-wiki strays (.ccstatusline)
# copy in, then stage deletions for any page removed from /wiki:
git -C wiki-publish add -A
git -C wiki-publish commit -m "docs(wiki): sync from /wiki @ <source-commit-sha>"
git -C wiki-publish push origin master
```

Preferred long-term: a GitHub Actions job on merge to `main` (e.g. `Andrew-Chen-Wang/github-wiki-action`
with `contents: write` and the default `GITHUB_TOKEN`) that publishes `/wiki` to the wiki repo so docs
ship atomically with code.
