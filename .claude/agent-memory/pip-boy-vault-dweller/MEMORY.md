## Setup

- Worktrees live at `D:/Development/Private/Frameworks/IntegratoR/.pip-boy/worktrees/<branch-name>/`
- Task index at `D:/Development/Private/Frameworks/IntegratoR/.pip-boy/index.json` (update status manually)
- Build: `dotnet build "<worktree>/IntegratoR.sln"` -- restore first if new packages added
- Test: `dotnet test "<worktree>/IntegratoR.sln" --no-build`
- Bash: Windows platform, use `dotnet` commands directly without `cd` -- use full absolute paths always
- Shell: `echo`, `ls`, `cd` fail; only dotnet/mkdir/git commands work reliably

## Patterns

- FluentAssertions 8.x custom assertions: constructor takes `(subject, AssertionChain.GetOrCreate())`, use `CurrentAssertionChain` (not `Execute.Assertion`), no `Execute` namespace needed
- Solution folders via `dotnet sln add` -- tool auto-creates `tests` folder and nests projects correctly using `{2150E333-...}` GUID type
- xUnit v3 warnings: use `client.GetAsync(new Uri(...), CancellationToken.None)` not `client.GetAsync("url")` to suppress xUnit1051
- All test files: file-scoped namespaces, XML doc comments on public members
- TestKit lives under `tests/IntegratoR.TestKit/` (class library, not test project), smoke tests in `tests/IntegratoR.TestKit.Tests/`
- RELion Base64 response pattern: inner JSON → Base64 → `RelionResponseEntity.EncodedResponseJson` → wrapped in `RelionResponsePayload.EntitySet`
- DI tests with `RelionAuthenticationHandler`: must register `IAuthenticator` mock to allow `CreateClient("RelionApiClient")` to activate the handler
- Worktree for shared integration tests: `test-suite-integration` branch (`pip-boy/test-suite/integration`), NOT separate per-mission worktrees
- `result.Value.Property.Should().Be(x)` works; `result.Value.Should().Be(x)` conflicts with custom ResultAssertionExtensions for complex types -- use FluentAssertions directly on properties
- xUnit v3 with ImplicitUsings does NOT auto-import `[Fact]`/`[Theory]` -- always add `using Xunit;` explicitly to test files
- `git stash` can corrupt working tree if there are unstaged changes from other agents -- always check `git status` carefully and restore with `git checkout <file>` if stash pops unexpected diffs

## Pitfalls

- `dotnet sln add` on Windows with multiple paths: pass all paths as space-separated args in single call -- works correctly
- FluentAssertions 8.x breaks `Execute.Assertion` pattern from 6.x -- must use `CurrentAssertionChain` property on the assertions class
- `dotnet restore` on solution returns exit code 1 when NU1900 warnings appear (Azure DevOps feed unreachable) -- NOT a true error, packages still restore
- Bash grep/pipe chains fail on Windows -- use `dotnet ... 2>&1` and let output flow; don't pipe through grep
- MediatR 12 `RequestHandlerDelegate<T>` takes a CancellationToken param -- lambdas must be `_ => Task.FromResult(...)` not `() => Task.FromResult(...)`
- NSubstitute proxy failure: test request types used as generic type args of `ILogger<Behaviour<TRequest, TResponse>>` must be `public` (not `internal`) in strong-named assemblies
- FluentValidation 12 `AddValidatorsFromAssembly`: does NOT register open-generic validators (e.g. `CreateCommandValidator<TEntity>`) -- only concrete types get registered; open-generic validators must be tested by direct instantiation
- `InMemoryCacheService.SetAsync(key, value, TimeSpan?)` -- no CancellationToken param; don't pass `CancellationToken.None` as 3rd arg

## Ranger Themes

- XML doc comments: Always add `<summary>` on all public members (entities, builder methods, fake helpers)
- File-scoped namespaces: Required on every .cs file
- CancellationToken: xUnit1051 -- use overloads with explicit CancellationToken in test code
