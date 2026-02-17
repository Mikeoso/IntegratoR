---
paths:
  - "**/*.cs"
  - "**/*.csproj"
  - "**/*.json"
---

# .NET Security

> This file extends [common/security.md](../common/security.md) with .NET-specific security practices.

## Azure Key Vault Integration

Access secrets through `IConfiguration` — the Azure Functions host binds Key Vault references automatically:

```csharp
// In settings classes, secrets come from configuration
services.Configure<ODataSettings>(configuration.GetSection("ODataSettings"));
// ODataSettings.ClientSecret is populated from Key Vault at runtime
```

Never add secrets to `local.settings.json` that gets committed. Use `local.settings.json` only for local development with test/dev credentials, and ensure it is in `.gitignore`.

## Authentication Pattern

The project uses `IAuthenticator` / `OAuthAuthenticator` for service-to-service OAuth:

- **MSAL** `ConfidentialClientApplicationBuilder` for client credentials flow
- Token caching via `IMemoryCache` with **5-minute pre-expiry buffer**:
  ```csharp
  var cacheExpiration = authResult.ExpiresOn.Subtract(TimeSpan.FromMinutes(5));
  _memoryCache.Set(tokenCacheKey, authResult.AccessToken, cacheExpiration);
  ```
- Transparent token injection via `ODataAuthenticationHandler` (a `DelegatingHandler` in the HttpClient pipeline)

For scaled-out Azure Functions, consider migrating to `IDistributedCache` instead of `IMemoryCache`.

## XML & OData Safety

The project enables DTD processing for D365 F&O metadata compatibility:

```csharp
AppContext.SetSwitch("Switch.System.Xml.AllowDefaultResolver", true);
```

This is safe because it only enables DTD parsing, not external entity resolution. The `ODataMetadataProvider` handles local metadata loading with proper error handling via `Result<T>`.

When working with XML/OData metadata:
- Prefer loading metadata from local files over fetching from remote endpoints
- Validate metadata content before parsing
- Never enable external entity resolution in XML parsers

## NuGet Package Security

Audit for vulnerable packages regularly:

```bash
dotnet list package --vulnerable
```

Pin package versions explicitly in `.csproj` files rather than using floating versions. Current key packages and their pinned versions:
- FluentResults 4.0.0
- MediatR 12.5.0
- FluentValidation 12.0.0
- Microsoft.Identity.Client 4.76.0

## Logging Safety

- Never log secrets, tokens, or credentials
- Be cautious with `{@Object}` destructuring in structured logging — it serializes the entire object
- Use specific property placeholders: `{RequestName}`, `{ElapsedMilliseconds}` instead of dumping full objects
- The `LoggingBehaviour` uses `{@Request}` for debug-level only — ensure sensitive fields are excluded from `GetLoggingContext()`
