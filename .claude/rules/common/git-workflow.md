# Git Workflow

## Branch Naming

Use `feature/<area>/<description>` for feature branches (e.g., `feature/odata/batch-operations`).
Use `fix/<area>/<description>` for bug fixes. Use `chore/<description>` for maintenance tasks.

## Commit Messages

- Write in imperative mood: "Add batch support" not "Added batch support"
- Keep the subject line under 72 characters
- Make commits atomic: one logical change per commit
- Reference related work in the body when relevant

## Pull Requests

- One concern per PR — do not mix features, refactors, and bug fixes
- Squash-merge feature branches into `main`
- Ensure CI passes before merging

## Versioning

This project uses **GitVersion** in `ContinuousDelivery` mode (see `GitVersion.yml`).
Never manually edit version numbers in `.csproj` files — versions are computed from git history.
The `main` branch produces clean release versions (no pre-release tags).
