# .NET Rules

## Runtime & Language

- **.NET 10** with `LangVersion=preview`, nullable enabled, implicit usings.
- File-scoped namespaces (`namespace Foo;`).
- **British spelling**: `Behaviour` not `Behavior` (intentional throughout codebase).

## Naming Conventions

- PascalCase for public members, `_camelCase` for private fields.
- `I` prefix for interfaces.
- Commands: `CreateCommand<T>`, `UpdateCommand<T>`, `DeleteCommand<T>`.
- Queries: `GetByKeyQuery<T>`, `GetByFilterQuery<T>`.

## Dependencies

- **Central Package Management**: all versions in `Directory.Packages.props`.
- Project files use `<PackageReference>` without version attributes.
- Do not add version attributes to project files.

## Testing

- **xUnit v3**, FluentAssertions, NSubstitute.
- Test projects mirror source project structure.
- Use `IntegratoR.TestKit` for shared test utilities:
  - `FakeCacheService`, `FakeHttpMessageHandler` for fakes.
  - `result.Should().BeSuccessful()` / `result.Should().BeFailed()` for Result assertions.

## Versioning

- **GitVersion** for semantic versioning (ContinuousDelivery mode).

## Patterns to Avoid

- No `var` for non-obvious types — prefer explicit types when the type is not apparent.
