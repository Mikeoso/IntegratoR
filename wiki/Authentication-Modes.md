# Authentication Modes

The OData layer supports two authentication modes selected via `ODataSettings.Authentication.Mode`. The mode is read at every HTTP request by `ODataAuthenticationHandler`, a `DelegatingHandler` injected at the top of the HTTP pipeline.

| Mode | Selector value | Used for | Wire mechanism |
|---|---|---|---|
| OAuth 2.0 | `"OAuth"` | Direct service-to-service calls to D365 F&O | `Authorization: Bearer <jwt>` header |
| API Key (APIM) | `"ApiKey"` | Calls fronted by Azure API Management | `Ocp-Apim-Subscription-Key: <key>` header (configurable name) plus optional extra headers |

The default `Mode` is `ApiKey` to match the typical APIM-fronted production topology. Direct OAuth deployments must set `Mode` explicitly.

## OAuth 2.0 (Client Credentials Flow)

```json
{
  "ODataSettings": {
    "Authentication": {
      "Mode": "OAuth",
      "OAuth": {
        "ClientId": "<azure-ad-app-id>",
        "ClientSecret": "<client-secret>",
        "TenantId": "<azure-ad-tenant-id>",
        "Resource": "https://your-environment.operations.dynamics.com"
      }
    }
  }
}
```

### Required Azure AD Setup

1. **Register an Azure AD app** in the same tenant as the D365 environment.
2. **Add an API permission** for *Dynamics ERP* (or *Microsoft Dataverse* for D365 CRM). Grant the appropriate role (typically *ODataApp.Connect* or *user_impersonation*).
3. **Grant admin consent** for the tenant.
4. **Create a client secret** in *Certificates & secrets*. The secret value is shown once — copy it immediately into Azure Key Vault (production) or local settings (development).
5. **Register the app as a service account in D365 F&O**: in the D365 UI go to *System administration → Setup → Azure Active Directory applications*, add a row with the Client ID and a meaningful user as the proxy identity.

### Token Acquisition

`OAuthAuthenticator` in `IntegratoR.Application` uses **MSAL** (`Microsoft.Identity.Client`) to acquire and cache tokens:

- The first request triggers `AcquireTokenForClient` against the tenant's `https://login.microsoftonline.com/<tenant>` endpoint, asking for the configured `<Resource>/.default` scope.
- Subsequent requests within the token's lifetime return the cached token from MSAL's in-memory cache.
- MSAL proactively refreshes tokens shortly before expiry (its default 5-minute buffer).
- Token acquisition failures surface as `IntegrationError("OData.AuthenticationFailed", <message>, ErrorType.Failure)` and short-circuit the HTTP pipeline — no request is sent to D365.

The `ODataAuthenticationHandler` returns an immediate HTTP 401 response (not a real network call) when token acquisition fails. The Polly retry policy does **not** fire for 401s, so the failure surfaces immediately to the consumer.

### Common OAuth Issues

| MSAL error code / symptom | Likely cause |
|---|---|
| `AADSTS70011: The provided value for the input parameter 'scope' is not valid` | `Resource` is wrong. For most D365 environments it is the environment URL **without** `/data`, e.g. `https://your-env.operations.dynamics.com`, not `https://your-env.operations.dynamics.com/data` |
| `AADSTS50034: The user account ... does not exist in the directory` | `TenantId` points at the wrong directory, or the service principal was registered in a different tenant |
| `AADSTS7000215: Invalid client secret provided` | Secret expired, was rotated, or has trailing whitespace; rotate and re-deploy |
| HTTP 401 returned by D365 itself (after token was acquired) | App registered in Azure AD but not registered as a service user inside D365 — see step 5 above |

## API Key (Azure API Management)

```json
{
  "ODataSettings": {
    "Authentication": {
      "Mode": "ApiKey",
      "ApiManagement": {
        "SubscriptionKey": "<apim-subscription-key>",
        "SubscriptionHeaderKey": "Ocp-Apim-Subscription-Key",
        "DefaultHeaders": {
          "x-routing-hint": "primary"
        }
      }
    }
  }
}
```

The handler simply appends the subscription key header on every outbound request. Extra static headers from `DefaultHeaders` are added in the same step (useful for APIM routing hints or trace correlation IDs that should propagate to every call).

### When to Use APIM

- APIM owns the authentication to D365 (typically with its own OAuth credentials configured in the API policy) and exposes the framework consumer a stable subscription-key surface.
- Multi-environment routing — a single APIM instance fronts dev / test / prod D365 environments and routes by header.
- Throttling and quota are managed at APIM rather than per-consumer.

The framework does not assume any specific APIM policy — it just adds the configured subscription key header. The APIM policy on the gateway side is the source of truth for what gets forwarded to D365.

## Secret Storage

| Environment | Recommended store |
|---|---|
| Local development | `local.settings.json` (gitignored, **never** committed) |
| Test / staging | Azure App Service configuration with Key Vault references |
| Production | Azure Key Vault, accessed via Managed Identity or `DefaultAzureCredential` |

The sample `Program.cs` shows the Key Vault wiring pattern using `Azure.Identity.DefaultAzureCredential` and an `Azure.Extensions.AspNetCore.Configuration.Secrets` provider — see [Set Up Azure Functions Host](Set-Up-Azure-Functions-Host).

> Never log `ClientSecret`, `SubscriptionKey`, or the acquired bearer token. The framework deliberately avoids structured-logging the entire `ODataSettings` instance for this reason — only `Url` and the `Mode` selector are visible at startup, never the credentials.

## Switching Modes at Runtime

The handler reads `Authentication.Mode` from `IOptions<ODataSettings>` on every request, so a programmatic override at startup is the supported way to switch modes per environment:

```csharp
services.AddIntegratoR(configuration, integrator =>
{
    integrator.ConfigureOData(settings =>
    {
        if (context.HostingEnvironment.IsDevelopment())
        {
            settings.Authentication.Mode = AuthenticationMode.OAuth;  // direct in dev
        }
        else
        {
            settings.Authentication.Mode = AuthenticationMode.ApiKey;  // APIM in prod
        }
    });
});
```

Per-request mode switching is not supported — the handler is wired once at HTTP client construction time.

## See Also

- [Configure OData](Configure-OData) — full settings reference
- [Set Up Azure Functions Host](Set-Up-Azure-Functions-Host) — Key Vault integration for secret storage
- [Troubleshoot Common Issues](Troubleshoot-Common-Issues) — common auth-error symptoms and resolutions
