# Common Rules

## Error Handling

- All responses use `FluentResults.Result<T>` — never throw exceptions for business flow control.
- Reserve exceptions for truly exceptional situations (network failures, null refs, etc.).

## Git Workflow

- **Branch naming**: `feature/<area>/<desc>`, `fix/<area>/<desc>`, `chore/<desc>`.
- **Commit style**: Imperative mood, under 72 chars. PRs are squash-merged.

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
