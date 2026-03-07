# Common Rules

## Error Handling

- All responses use `FluentResults.Result<T>` — never throw exceptions for business flow control.
- Reserve exceptions for truly exceptional situations (network failures, null refs, etc.).

## Git Workflow

1. Create a feature branch from `main` before starting work.
2. Implement changes on the feature branch.
3. Create a PR targeting `main`.
4. Push the branch to publish the PR.

- **Branch naming**: `feature/<area>/<desc>`, `fix/<area>/<desc>`, `chore/<desc>`.
- **Commit style**: Imperative mood, under 72 chars. PRs are squash-merged.

## Task Delegation

After implementing or modifying code, delegate review to specialised subagents:
- Code changes -> use `code-reviewer` subagent
- Auth, credential, or security-sensitive code -> use `security-reviewer` subagent
- Before creating PRs -> run both reviewers

Do not perform these reviews directly. Always delegate to the appropriate specialist.

## Formatting

- `dotnet format` is enforced via a PostToolUse hook that runs automatically on Write/Edit.
- Do not add manual formatting rules — the hook and `.editorconfig` handle this.

## Sensitive Files

- Never read `.env` or `.env.*` files (denied in settings).
- `local.settings.json` contains Azure Functions local config — do not commit.

## Code Quality

- Do not add comments, docstrings, or type annotations to code you did not change.
- Do not create abstractions for one-time operations.
- Do not add error handling for scenarios that cannot happen.
- Only validate at system boundaries (user input, external APIs).

## Verification

- Run `dotnet build` before marking work complete.
- Run relevant tests (`dotnet test` or filtered) to prove changes work.
- If the change has observable behaviour, demonstrate it — don't just assume correctness.

## Problem Solving

- If an approach fails or produces unexpected results, stop and reassess. Do not patch forward.
- When fixing bugs, investigate root causes. Do not apply temporary patches or workarounds.
- Read logs, errors, and failing tests before asking the user for guidance.
