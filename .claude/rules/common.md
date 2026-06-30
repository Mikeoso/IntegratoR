# Common Rules

Workflow and working-discipline rules. C# language, style, error-handling, and code-quality conventions live in the `csharp-coding-standards` skill; the `Result<T>` never-throw invariant is a CLAUDE.md Hard Rule.

## Git Workflow

1. Create a feature branch from `main` before starting work.
2. Implement changes on the feature branch.
3. Create a PR targeting `main`.
4. Push the branch to publish the PR.
5. **Review gate** — the `claude-review` GitHub Action is the gate, run as two rounds: open PR (round 1) → address findings → push to re-trigger (round 2) → proceed when clean. A green `build` check is **necessary but NOT sufficient**. Local `code-reviewer` / `security-reviewer` agents are a *complement* (fast pre-PR feedback), **not a substitute** for `claude-review`; if substituting is ever unavoidable, say so and get explicit sign-off first.
6. **Merge** only once `claude-review` is clean. Admin-merge (squash) is used because the solo author can't satisfy `main`'s 1-approving-review rule — it bypasses *that approval rule only*, **never** the `claude-review` or `build` checks. Confirm before each admin-merge (it bypasses branch protection).

- **Branch naming**: `feature/<area>/<desc>`, `fix/<area>/<desc>`, `chore/<desc>`.
- **Commit style**: Imperative mood, under 72 chars. PRs are squash-merged.

## Formatting

- `dotnet format` is enforced via a PostToolUse hook that runs automatically on Write/Edit.
- Do not add manual formatting rules — the hook and `.editorconfig` handle this.

## Verification

- Run `dotnet build` before marking work complete.
- Run relevant tests (`dotnet test` or filtered) to prove changes work.
- If the change has observable behaviour, demonstrate it — don't just assume correctness.

## Problem Solving

- If an approach fails or produces unexpected results, stop and reassess. Do not patch forward.
- When fixing bugs, investigate root causes. Do not apply temporary patches or workarounds.
- Read logs, errors, and failing tests before asking the user for guidance.
