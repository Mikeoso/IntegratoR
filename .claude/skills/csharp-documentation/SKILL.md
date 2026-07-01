---
name: csharp-documentation
description: >-
  Use when writing, reviewing, trimming, or fixing CODE documentation in C# — XML
  doc comments (/// <summary>, <param>, <returns>, <typeparam>, <remarks>,
  <exception>, <inheritdoc>, <see>) and inline // comments. Enforces IntegratoR's
  lean-by-default doc philosophy (document the general logic concisely; inline only
  when truly necessary; no essays, no task/changelog narratives), 100% public
  coverage on packable libraries, BCL house wording, British spelling, the
  Result<T> failure-in-<returns> rule, and the ban on FILE-LEVEL banner comments.
  Do NOT use for wiki/README/architecture prose (see the /docs command) or non-C#.
---

# C# Code Documentation

Doc comments and inline comments in `.cs` files. Not wiki, not README, not architecture prose.

## The philosophy: lean by default

**Document the general logic concisely. Inline-comment only when a reader genuinely needs the
WHY. Never write essays, never narrate the code, never record task/work state in a doc.** A normal
developer documents *purpose and contract*, not a tutorial. When in doubt, cut.

The shipped `.xml` is the consumer's **only** view of us in IntelliSense — so a doc that is
missing, verbose, or *wrong* is a defect. Cover the public surface, but make every word earn its place.

Two rules that resolve every judgement call:
- **Coverage** applies to the **public/protected surface of packable libraries** — one concise
  `<summary>` minimum. (Compiler-enforced via CS1591.)
- **Depth** is earned, not default — a one-line `<summary>` is the norm; `<remarks>` / inline `//`
  are the *exception*, used only where real contract nuance or a non-obvious WHY exists.

## When to document — and when not

| Surface | Rule |
|---|---|
| Public / protected in a **packable** lib (`Abstractions`, `Application`, `OData`, `OData.FO`, `Hosting`) | **Mandatory** `///` with ≥ `<summary>`. |
| `internal` / `private` in a packable lib | **No `///`** — it ships nothing and leaks internals. Inline `//` only for a non-obvious WHY. |
| **CodeGen** (packed as a *tool*, not a consumed lib) | No coverage mandate. Keep useful docs; delete echo/noise comments. |
| Tests, SampleFunction, Client.Sample (`IsPackable=false`) | No mandate — test *names* are the docs. |

**Do NOT write** a comment that:
- restates the code (`// increment i` over `i++`; `// Key attribute` over `[Key]`);
- re-spells the member name (`CreateCommandHandler` → "handler that handles CreateCommand");
- records task/work/debug state or a changelog ("was wrong, caused JsonException on 2026-04-27…") — that is git's job;
- is a banner / divider / attribution block (see Anti-patterns);
- is left **empty** (`<returns></returns>`) — an empty tag is worse than none (looks done, says nothing);
- is commented-out code — delete it.

## XML tags — purpose and misuse

| Tag | For | Wrong when |
|---|---|---|
| `<summary>` | One–two sentence "what", shown in IntelliSense. | Paragraphs (→`<remarks>`); echoing the name. |
| `<remarks>` | *Exception-only* extended why/how: contract nuance, threading, defaults, D365/OData quirks, security caution. | Padding trivial members; re-narrating the body as a numbered list; tutorials. |
| `<param name="x">` | One per parameter, name matching signature **exactly** (compiler-verified). Effect/constraints/units. | No such parameter; restating the type. |
| `<typeparam name="T">` | One per generic type param — what it represents. | Copy-paste stale text (e.g. "to create" on an *update*). |
| `<returns>` | Method return. For `Result<T>`: success value **and** modelled failure conditions/error codes. | On `void`/`Task`; on properties. |
| `<value>` | What a property holds + default/range — preferred for options/settings types. | On methods. |
| `<exception cref>` | **Genuine throws only**; one per type; multiple causes of one type joined by `-or-`. | For modelled `Result<T>` failures — those go in `<returns>`/`<remarks>`. |
| `<inheritdoc/>` | Interface impls, overrides, sync/async twins with an **unchanged** contract. **Required** for packages (the compiler `.xml` does *not* auto-inherit like the VS IDE). | Behaviour diverges from base (write bespoke); nested inside `<summary>` (must stand alone). |
| `<see cref>` / `<seealso cref>` | Link code; brace generics `cref="IService{TEntity}"`; `<see langword="true/false/null"/>`. | Bare type names; markdown backticks; `href` for code. |
| `<example><code language="csharp">` | A **compiling** snippet — only on main entry points (`AddIntegratoR`, `IntegratoRBuilder`, sending a command via `IMediator`). | A snippet that does not compile; `<example>` with no `<code>`. |
| `<c>` | Inline code fragment. | Markdown backticks (use `<c>`). |

## Wording canon (BCL / Framework Design Guidelines)

| Member | Template |
|---|---|
| Class/struct (state) | `Represents <thing>.` |
| Class/struct (service) | `Provides <functionality>.` |
| Interface | `Defines <contract>.` |
| Constructor | `Initializes a new instance of the <see cref="Type"/> class.` (+`… using the specified <x>.` per overload) |
| Read/write property | `Gets or sets <noun phrase>.` (describe the value, not the CLR type) |
| Read-only property | `Gets <noun phrase>.` (no "read-only" note) |
| Boolean property | `Gets [or sets] a value indicating whether <affirmative condition>.` |
| Method (sync) | verb-first: `Removes the entity that matches the specified composite key.` |
| Method (async) | `Asynchronously …` |
| Boolean return | `<returns><see langword="true"/> if <condition>; otherwise, <see langword="false"/>.</returns>` (**true IF**) |
| Non-boolean return | noun phrase with article, no type name: `The created entity with server-generated fields populated.` |
| `Result<T>` return | `A successful <see cref="FluentResults.Result{T}"/> with …; a failed result with <c>ErrorType.Validation</c> when …` |
| Parameter (non-bool) | noun phrase, exact name: `<param name="cancellationToken">A token that cancels the outbound OData request.</param>` |
| Parameter (bool) | `<see langword="true"/> to <action>; otherwise, <see langword="false"/>.` (**true TO**) |
| Exception | `Thrown when <paramref name="entity"/> is <see langword="null"/>.` |
| Enum type / member | `Specifies <what it selects>.` / each member one sentence, **parallel** wording across siblings. |
| Type parameter | `The type of the entity being operated on.` |
| Fluent/builder method | `<returns>The same builder instance.</returns>` |
| Override, unchanged contract | `<inheritdoc/>` standalone. |

## Inline comments (`//`)

**Default to NO comment. The code is the primary documentation.** An inline comment earns its place
only when it says something the code cannot: a non-obvious WHY, an edge case, a workaround, a
D365/OData quirk, an invariant, or a subtle control-flow reason. A normal developer documents the
*general logic* — not a running narration of what each line does.

**Keep them SHORT.** Prefer a few words. If you need more than ~2 lines to explain a WHY, the
explanation is too big for an inline comment — move it (see "Long rationale" below).

### Acceptable
- A terse section signpost that chunks a genuinely long method: `// Parse key property names`, `// Entity-level annotations`. A few words, aids scanning — keep these.
- A short non-obvious WHY: `// 5-min buffer absorbs clock skew before the token actually expires.`
- An edge-case / quirk note: `// D365 returns 204 on a composite-key PATCH, so echo the caller's entity.`

### Delete on sight (obvious-code narration & noise)
- A prose sentence restating the next line: `// Attempt to retrieve the response from the cache using the key defined in the query.` above `_cacheService.GetAsync(query.CacheKey)`.
- A comment restating a self-evident branch: `// This only acts on queries that opt into caching.` above `if (request is not ICacheableQuery …)`.
- A trailing comment on an obvious statement: `return cached; // short-circuit and return the cached value.`
- **State-of-development / changelog narration**: "currently dormant", "was wrong, caused …", "TODO: refactor later" without an owner+issue. Git and the issue tracker own history.
- **Walls of text**: a 6-line paragraph explaining framework internals inline (see "Long rationale").

### Long rationale — move it out
When the WHY is genuinely important but too big for one short line:
- **Consumers need it** (it explains observable behaviour of a public API) → put it in the member's XML `<remarks>`.
- **Internal design decision** (why the composition root closes open generics, why we forked an upstream parser, how the Durable Functions converter is wired) → write an **ADR** under `docs/adr/` and leave a one-line pointer in code: `// Why this closes open generics across assemblies: see docs/adr/0001-....md`. See `docs/adr/README.md` for the format.

`TODO`/`FIXME`/`HACK` get an owner + a tracked issue and stay in `//` — **never** in a shipping `///`.

### Finding these (audit heuristic)
Suspect any run of **2+ consecutive `//` lines**, any `//` that is a **full grammatical sentence**
ending in a full stop describing the *next* statement, any trailing `// …` on an obvious line, and
any comment containing "currently", "for now", "simple", "just", "note that", or restating an
identifier that already appears on the following line.

## Anti-patterns (all found live in this repo — remove on sight)

1. **`// FILE-LEVEL DOCUMENTATION` banner blocks** — `<remarks>` inside `//` is inert, redundant, drifts stale. Delete; fold any unique fact into the type `<summary>`.
2. **Stale generic-arity / phantom docs** — `IEntity<TKey>`, `GetByIdQuery<TEntity,TKey>` don't exist; the real types are non-generic `IEntity` / `GetByKeyQuery<TEntity>`.
3. **Forbidden-term docs** — never call a service a "repository" (Hard Rule: no repository pattern).
4. **American spelling in prose** — `behavior`/`optimize`/`serialization`/`sanitize`/`centralized` → British (`behaviour`, `optimise`, `serialisation`, `normalise`, `honour`).
5. **Obvious-code narration** — a prose `//` sentence restating what the next line plainly does, or a trailing `// …` on an obvious statement. Delete. (A *terse* few-word section signpost in a long method — `// Parse properties` — is fine; it's the full-sentence narration and walls of text that go. See the Inline comments section.)
6. **Changelog / debug history in shipped docs** — keep only the forward-looking fact; history lives in git.
7. **Duplicated overload blocks** — differentiate summaries or `<inheritdoc cref="…"/>` the twin.
8. **Boilerplate filler** — allowed only in the canonical `Initializes a new instance…` form.
9. **`<b>`/markdown in XML** — use `<c>`/`<see>`; escape `<`,`>` as `&lt;`,`&gt;` (brace syntax in crefs); escape `F&O` as `F&amp;O`.
10. **Verbose essay `<remarks>`** — first-person-plural tutorials ("we can create…"), numbered step-by-step restatements of the body. Trim to the contract fact a caller needs, or delete.

## IntegratoR invariants

- **`Result<T>` is the failure channel** — document failure conditions/error codes in `<returns>`/`<remarks>`; reserve `<exception>` for genuine throws (argument guards, missing `IHttpClientFactory`, I/O in CodeGen).
- **British spelling in docs** (matches the code Hard Rule).
- **Docs are versioned public contract** (`api-compatibility.md`) — update `<param>`/`<typeparam>`/`cref` and add the `[Obsolete]("since vX.Y; use …")` pointer in the *same* change as any signature/visibility change. A stale doc misleads every consumer's IntelliSense.

## Build enforcement

Packable libs set `<GenerateDocumentationFile>true</GenerateDocumentationFile>` so the `.xml` ships
and CS1591 flags undocumented public members. **Success = zero CS1591 on the packable projects.**
Not enabled on tool/test/sample projects.
