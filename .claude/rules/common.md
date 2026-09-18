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

## Hotfix / Support Lines

For a consumer stuck on an older released version that cannot absorb the breaking changes between
it and `main` — typically a production system several MAJORs behind.

1. Branch `support/<major>.<minor>` from the **release tag**, never from `main`.
2. Fix + tests on a `fix/...` branch, PR targeting the support branch.
3. Release via `workflow_dispatch` with `release: true` **on the support branch**.

- **Never merge in either direction.** Merging `main` into a support branch makes newer tags
  reachable, and GitVersion's tag clamp then raises the computed version off the hotfix line.
  The fix must already exist on `main` before the support line ships it — a support line never
  carries something `main` lacks.
- **Strictly PATCH.** No new public API, no signature change, no `[Obsolete]` — see
  `api-compatibility.md`.
- **Nothing is published on push.** Only `main` publishes on push; a support branch publishes solely
  on an explicit `workflow_dispatch`. A maintenance line has no audience for a running prerelease
  stream, and it keeps a half-finished hotfix off the public feed — which matters because a NuGet
  package can be unlisted but never deleted.
- **Versioning is branch-local.** `GitVersion.yml` is a file in the branch, so the support branch
  carries its own. `next-version` is root-only (not valid under `branches:`) and acts as a *floor*:
  set it to the intended hotfix version when the naturally computed patch would collide with a tag
  already taken on another line. Give the branch an explicit `support` entry with
  `mode: ContinuousDelivery` — without it the branch falls through to `unknown`/`ManualDeployment`
  and pre-release versions stop incrementing per commit, so two dispatched prereleases would carry
  the same version and `dotnet nuget push --skip-duplicate` would swallow the second silently.
- **The review gate is unchanged.** `build` and `claude-review` both run on PRs targeting
  `support/**`; give the branch the same protection rules as `main`.
- **A hotfix never claims "Latest".** It ships after the newer main-line releases, so the publish
  workflow marks the GitHub Release latest only on `main`.

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
