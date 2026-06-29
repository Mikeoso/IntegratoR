# Contributing to IntegratoR

Thank you for your interest in contributing to IntegratoR! This document provides guidelines and information to help you get started.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local) (for running functions locally)
- Git

### Setting Up Your Development Environment

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/<your-username>/IntegratoR.git
   cd IntegratoR
   ```
3. Build the solution:
   ```bash
   dotnet build
   ```

## Branch Naming

| Prefix | Purpose | Example |
|--------|---------|---------|
| `feature/<area>/<description>` | New features | `feature/odata/batch-operations` |
| `fix/<area>/<description>` | Bug fixes | `fix/auth/token-refresh` |
| `chore/<description>` | Maintenance | `chore/update-dependencies` |

## Making Changes

1. Create a branch from `main` following the naming conventions above
2. Make your changes in focused, atomic commits
3. Write commit messages in imperative mood: "Add batch support" not "Added batch support"
4. Keep the subject line under 72 characters

## Code Style

- **British spelling is intentional** throughout the codebase (e.g., `Behaviour` not `Behavior`)
- Enable nullable reference types in all new code
- Follow the existing patterns found in `.claude/rules/dotnet/`
- Use `Result<T>` from FluentResults for operation outcomes (no exceptions for flow control)
- Propagate `CancellationToken` through all async call chains
- Use `ConfigureAwait(false)` in library code

## Architecture

This project follows **Clean Architecture** with dependencies pointing inward:

```
SampleFunction (host) -> Application -> Abstractions (core)
                      -> OData -> Abstractions
                      -> OData.FO -> OData -> Abstractions
```

- **Abstractions** contains domain interfaces, entities, and CQRS contracts
- **Application** contains use cases and pipeline behaviours
- **OData** contains the generic OData client and resilience infrastructure
- **OData.FO** contains the D365 F&O entity models and handlers
- **SampleFunction** is the composition root (Azure Functions host)

## Pull Requests

1. Ensure your branch is up to date with `main`
2. Verify the build passes: `dotnet build`
3. Run the formatter: `dotnet format --verify-no-changes`
4. Open a pull request against `main`
5. Fill out the PR template completely
6. One concern per PR — do not mix features, refactors, and bug fixes
7. PRs are squash-merged into `main`

## Reporting Issues

- Use the **Bug Report** template for bugs
- Use the **Feature Request** template for new ideas
- Search existing issues before creating a new one

## Security

If you discover a security vulnerability, **do not open a public issue**. Please follow the process described in [SECURITY.md](SECURITY.md).

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
