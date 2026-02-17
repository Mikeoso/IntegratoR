# IntegratoR

.NET 10 integration framework using Clean Architecture, CQRS (MediatR), FluentResults, and Azure Functions (Durable Tasks). Targets D365 Finance & Operations and RELion via OData.

## Rules

Project conventions are defined in `.claude/rules/`:

- **`common/`** — Language-agnostic rules: git workflow, performance, security, testing
- **`dotnet/`** — .NET-specific rules: coding style, patterns, testing, hooks, security (path-scoped to `*.cs`, `*.csproj`, `*.json`)

## Key Commands

```bash
dotnet build                    # Build the solution
dotnet format --no-restore      # Format code (default rules, no .editorconfig yet)
dotnet test                     # Run tests
func start                     # Run Azure Functions locally (from SampleFunction dir)
```
