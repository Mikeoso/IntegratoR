# IntegratoR Claude Code Setup

This directory configures Claude Code for IntegratoR. It is checked into the repo, so the setup travels with a clone.

## Layout

| Path | Loaded | Purpose |
|------|--------|---------|
| `../CLAUDE.md` | always | The hub: domain anchor, tech stack, repo map, the load-bearing hard-rule tripwires, default workflow, commands, and skill/agent routing. |
| `rules/*.md` | always | Always-on policy: domain, architecture, reliability, security, workflow. Auto-discovered and injected every session — keep terse. |
| `skills/*/SKILL.md` | on-demand | Detailed how-to + worked examples for a recognizable authoring task. Loaded only when the description matches the work. |
| `agents/*.md` | delegated | Subagents that run in their own context for handed-off tasks (code review, security review, test writing). |
| `scripts/*.ps1` | hooks | PowerShell invoked by the hooks in `settings.json` (destructive-command guard, auto-format). |
| `settings.json` | — | Permissions, hooks, plugins, MCP servers, status line. |

## The placement rule

Decide with two questions: *is it always needed?* and *is it tied to a recognizable authoring task?*

- **A one-line invariant that must never be silently broken, whatever the task** → `CLAUDE.md` Hard Rules (always-on tripwire).
- **Always-relevant domain / architecture / reliability / security / workflow policy** → a `rules/*.md` file (always-on).
- **Detailed how-to + worked examples for a recognizable authoring task** (write C#, design a public API, write tests) → a `skills/*` skill (on-demand).
- **A task you hand off and consume a verdict from** → an `agents/*.md` subagent.

### Ownership

- **One owner per fact.** No copy lives in two files. The only allowed layering is *altitude*: an invariant's one-line WHAT in `CLAUDE.md` vs its worked-example HOW in a skill — cross-referenced, never restated.
- Coding standards (language, style, naming, testing how-to) live in the `csharp-coding-standards` skill, not in a rules file. Their must-never-silently-break tripwires stay in `CLAUDE.md`.
- A rules file that becomes pure coding-standards is **retired**, not left as a pointer husk.

### Current owners

| Topic | Owner |
|-------|-------|
| C# language / style / naming / testing how-to | `skills/csharp-coding-standards` |
| GitHub wiki conceptual / how-to / reference prose | `skills/wiki-documentation` (via the `/docs` command) |
| Public API compatibility & versioning | `rules/api-compatibility.md` + `skills/csharp-api-design` |
| Architecture, CQRS, serialisation | `rules/architecture.md` |
| OData / D365 entity & settings conventions | `rules/odata-conventions.md` |
| HTTP / resilience / caching / logging | `rules/perf-reliability.md` |
| Secrets / auth / header safety | `rules/security.md` |
| Git / formatting / verification / problem-solving | `rules/common.md` |
| Hard-rule tripwires, map, routing | `../CLAUDE.md` |

## Adding a skill

1. **Validate** the candidate against the live codebase — does it match, where does it conflict?
2. **Adapt** it to IntegratoR: keep what matches, rewrite conflicts, drop irrelevant content.
3. **Place** per the rule above: coding-standards detail in the skill; always-on invariants as `CLAUDE.md` tripwires; domain/workflow in rules.
4. **Consolidate**: retire or slim any rules content the skill now owns. One owner per fact.
5. **Route**: add the skill to the `Skills & Agents` table in `CLAUDE.md`.
