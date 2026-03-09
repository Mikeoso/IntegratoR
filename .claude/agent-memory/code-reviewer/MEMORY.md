# Code Reviewer Memory

## Project Quick Facts
- All library code requires `ConfigureAwait(false)` and `CancellationToken` propagation
- British spelling: `Behaviour` not `Behavior`
- File-scoped namespaces required: `namespace Foo;` — block-scoped braces are a violation
- Central Package Management: no version attributes in `<PackageReference>` elements
- No `var` for non-obvious types
- FluentResults only: `Result<T>` / `Result` — no exceptions for business logic

## Architecture Layers (dependency direction, inner -> outer)
```
Abstractions <- Application, OData, RELion
Abstractions <- OData <- OData.FO
Abstractions, OData, OData.FO, RELion <- Hosting (new, composition layer)
Hosting, RELion <- SampleFunction (host / composition root)
```

## Key Patterns
- DI extension methods live in `Common/Extensions/ApplicationDependencyInjection.cs` per layer
- RELion's `ApplicationDependencyInjection` remains `public` — it is consumed directly by SampleFunction
- IntegratoR.Hosting is a new composition helper layer; the three core layer DI methods were made `internal` and exposed via `InternalsVisibleTo`
- `IntegratoRBuilder` provides a fluent API: `AddConsumerHandlers`, `ConfigureOData`, `ConfigureFO`
- `PostConfigure<T>` is used (not `Configure<T>`) for builder overrides so they apply after config binding

## Known Issues Seen in Review (feature/commands/add-docs-command era)
- OData.FO `ApplicationDependencyInjection.cs` still uses block-scoped namespace `{ }` — violates file-scoped namespace rule
- Tests in `IntegratoR.Hosting.Tests` do not use TestKit Result assertions (`result.Should().BeSuccess()`) — not applicable here because there are no Result-returning methods under test, so no finding
- `services.PostConfigure(builder.ODataPostConfigure)` calls are missing the generic type argument — they rely on type inference from `Action<T>`, which works but is worth watching if overloads proliferate

## See Also
- patterns.md (to be created if recurring patterns emerge)
