---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---

# Known Pitfalls

- **FluentAssertions 8.x:** Use `CurrentAssertionChain` not `Execute.Assertion` (6.x pattern)
- **MediatR 12:** `RequestHandlerDelegate<T>` takes a `CancellationToken` param — lambdas need `_ => Task.FromResult(...)`
- **FluentValidation 12:** `AddValidatorsFromAssembly` skips open-generic validators — test those by direct instantiation
- **xUnit v3 + ImplicitUsings:** Must add `using Xunit;` explicitly — not auto-imported
- **NSubstitute:** Types used as `ILogger<>` generic args must be `public` (not `internal`)
- **InMemoryCacheService:** `SetAsync(key, value, TimeSpan?)` — no CancellationToken param
- **dotnet restore:** Exit code 1 on NU1900 warnings (unreachable feed) is not a true error
