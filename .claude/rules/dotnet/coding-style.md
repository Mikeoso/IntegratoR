---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---

# .NET Coding Style

> This file extends [common rules](../common/) with .NET-specific conventions.

## Project Settings

All projects in this solution use these settings — never change them:

```xml
<TargetFramework>net10.0</TargetFramework>
<LangVersion>preview</LangVersion>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
```

## File Structure

Every `.cs` file must use **file-scoped namespaces** (single-line, no braces):

```csharp
namespace IntegratoR.Application.Common.Behaviours;
```

Add a **FILE-LEVEL DOCUMENTATION** block between the namespace and the first type declaration for important files:

```csharp
namespace IntegratoR.Application.Common.Behaviours;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// Brief explanation of why this file exists and its role in the architecture.
// </remarks>
// ---------------------------------------------------------------------------------------------
```

## Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Private fields | `_camelCase` | `_logger`, `_memoryCache` |
| All methods | `PascalCase` | `Handle`, `GetByKeyAsync` |
| Interfaces | `I` prefix | `IService<T>`, `IContext` |
| Abstract classes | `Base` prefix | `BaseEntity<TKey>` |
| Pipeline behaviours | British spelling: `Behaviour` | `LoggingBehaviour`, `ValidationBehaviour` |
| Async methods | `Async` suffix | `GetAccessTokenAsync` |
| DI entry point | `ApplicationDependencyInjection` | One per project, static class |
| DI method | `Add{Feature}` | `AddODataClient`, `AddApplicationServices` |

**Important:** This project uses British spelling `Behaviour` (not `Behavior`). Never "correct" this — it is intentional and consistent across the codebase.

## Error Handling

This project uses **FluentResults** (`Result<T>`) for all operation outcomes. Do not use exceptions for business logic flow.

```csharp
// Returning success
return Result.Ok(entity);

// Returning failure
return Result.Fail(new Error("ErrorCode.Specific")
    .WithMetadata("detail", "Human-readable message"));

// Checking results
if (result.IsFailed)
{
    // Handle failure path
}
```

- Every handler, service method, and public API returns `Result` or `Result<T>`
- Use `Result.Fail()` for expected failures (validation, not found, conflict)
- Let exceptions propagate only for truly unexpected errors
- Pipeline behaviours catch and log exceptions but re-throw them

## Modern C# Features

Prefer these patterns throughout the codebase:

- `required` keyword for mandatory properties on DTOs
- `record` types for CQRS commands and queries (immutable by design)
- Pattern matching with `is`, `switch` expressions, and property patterns:
  ```csharp
  if (response is Result { IsFailed: true } result) { ... }
  ```
- `ConfigureAwait(false)` on all `await` calls in library projects (non-Function projects)
- Collection expressions where appropriate: `string[] scopes = [$"{resource}/.default"];`
- Primary constructors for simple DI injection in services

## XML Documentation

- All public types and members **must** have `<summary>` XML doc comments
- Use `<inheritdoc />` on interface implementations to avoid duplication
- Include `<remarks>` for architectural context on important types
- Include `<param>` and `<returns>` on public methods

```csharp
/// <summary>
/// A MediatR pipeline behavior that provides structured logging for all requests.
/// </summary>
/// <typeparam name="TRequest">The type of the MediatR request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public class LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IContext
```

## IntegrationError Codes

`IntegrationError` extends FluentResults `Error` with a structured `Code` and `ErrorType`. Follow this naming convention:

**Code format:** `Area.SpecificError`

| Area | Example Code | When to Use |
|---|---|---|
| `CompanyOrchestrator` | `CompanyOrchestrator.InvalidInput` | Orchestrator-level validation failures |
| `CompanyOrchestrator` | `CompanyOrchestrator.MissingBatchNumber` | Missing expected data after an activity call |
| `BlobStorage` | `BlobStorage.ReadFailed` | Azure Blob Storage operation failures |
| `OData` | `OData.CreateFailed` | OData POST/PATCH failures |
| `Mapping` | `Mapping.AccountNotFound` | Lookup failures during data mapping |

**ErrorType enum** maps to HTTP semantics for consistent handling:

| ErrorType | Meaning | Use When |
|---|---|---|
| `Validation` | Bad input, missing required fields | Input fails validation before processing |
| `NotFound` | Entity or mapping not found | Lookup returns no results |
| `Conflict` | Duplicate or state conflict | Entity already exists, concurrent modification |
| `Failure` | General operational failure | External system error, unexpected state |
