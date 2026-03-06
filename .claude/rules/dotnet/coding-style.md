---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---

# .NET Coding Style

## Project Settings (never change)

```xml
<TargetFramework>net10.0</TargetFramework>
<LangVersion>preview</LangVersion>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
```

## Naming

- British spelling: `Behaviour` not `Behavior` — intentional and consistent
- Private fields: `_camelCase`
- DI entry point: one `ApplicationDependencyInjection` static class per project with `Add{Feature}` methods
- Async methods: `Async` suffix

## File Structure

- File-scoped namespaces on every `.cs` file
- XML doc comments (`<summary>`) on all public types and members
- `<inheritdoc />` on interface implementations

## IntegrationError Codes

Format: `Area.SpecificError` (e.g., `OData.CreateFailed`, `Mapping.AccountNotFound`)

ErrorType enum: `Validation`, `NotFound`, `Conflict`, `Failure`

## Authentication Pattern

MSAL client credentials with **5-minute pre-expiry buffer** on cached tokens. Token injection via `ODataAuthenticationHandler` (DelegatingHandler).
