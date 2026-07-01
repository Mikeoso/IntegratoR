# Authentication Modes
> Last verified against v2.0.1

The OData layer authenticates to D365 F&O one of two ways, selected by `ODataSettings.Authentication.Mode`: **OAuth** (a Bearer token acquired directly from Azure AD) or **ApiKey** (a subscription key for an Azure API Management gateway). `ODataAuthenticationHandler` reads the mode on every outgoing request and attaches the matching header.

The default mode is `ApiKey`. Set it explicitly — a deployment that omits the mode falls to `ApiKey` and fails validation at startup if no subscription key is present.

## Configure OAuth (client credentials)

Bind these under `ODataSettings` in `local.settings.json` (development) or Azure App configuration (deployed):

```json
{
  "ODataSettings": {
    "Url": "https://your-environment.operations.dynamics.com/data",
    "Authentication": {
      "Mode": "OAuth",
      "OAuth": {
        "ClientId": "<azure-ad-app-id>",
        "ClientSecret": "<from-key-vault>",
        "TenantId": "<azure-ad-tenant-id>",
        "Resource": "https://your-environment.operations.dynamics.com"
      }
    }
  }
}
```

`AddIntegratoR(configuration)` binds the section and wires the handler. Each value's source:

| Value | Where it comes from |
|---|---|
| `ClientId` | Application (client) ID of the Azure AD app registration |
| `ClientSecret` | A secret from the app registration → *Certificates & secrets*. Store in Key Vault; never commit it |
| `TenantId` | The directory (tenant) ID that owns the app registration and the D365 environment |
| `Resource` | The environment URL **without** `/data` — the token audience, not the OData root |

`OAuthAuthenticator` (`IntegratoR.Application`) acquires the token via MSAL (`Microsoft.Identity.Client`) using the client credentials flow, requesting the `{Resource}/.default` scope against `https://login.microsoftonline.com/{TenantId}`. It caches the token in `IMemoryCache` and refreshes it five minutes before expiry, so most requests reuse a cached token rather than call Azure AD.

Register the app as a service user inside D365 as well: *System administration → Setup → Azure Active Directory applications*, adding a row with the `ClientId` and a proxy identity. Without that row D365 returns its own 401 even after the token is acquired.

> [!NOTE]
> The MSAL token cache lives in `IMemoryCache`, local to one instance. A scaled-out host acquires a token per instance. Front MSAL with a distributed token cache if that matters for your throughput.

### Handle the failure path

Token acquisition never throws. A failure returns a failed `Result<string>` carrying an `IntegrationError`, and `ODataAuthenticationHandler` short-circuits the request:

```csharp
Result<string> token = await authenticator.GetAccessTokenAsync(
    clientId, clientSecret, tenantId, resource, cancellationToken);

if (token.IsFailed)
{
    IError error = token.GetError();
    // error.Code    == "Auth.Msal.invalid_client"  (Auth.Msal.{MSAL error code})
    // error.Message == "Token acquisition failed"
    // ((IntegrationError)error).Type == ErrorType.Failure
    // ((IntegrationError)error).Exception holds the MsalException for server-side logging
}
```

The error `Code` is `Auth.Msal.{code}` where `{code}` is the MSAL error code (for example `invalid_client`, `unauthorized_client`). The full MSAL message — which can carry AADSTS codes and tenant IDs — stays on the inner exception for server-side logging, never in `error.Message`.

On the HTTP path, the handler returns an immediate `401 Unauthorized` with `ReasonPhrase "Authentication failed"` and no request reaches D365. That response carries no error code and no MSAL detail. Polly does not retry a 401, so the failure surfaces to the caller at once.

> [!WARNING]
> Never surface `IntegrationError.Exception` or the raw MSAL message in an HTTP response. The 401 `ReasonPhrase` is deliberately generic to keep tenant IDs and AADSTS codes out of client-visible output.

## Configure API Key (APIM)

Use `ApiKey` when Azure API Management fronts D365 and owns the authentication to it. The framework attaches only the subscription-key header; the APIM policy decides what reaches D365.

```json
{
  "ODataSettings": {
    "Url": "https://your-apim.azure-api.net/d365/data",
    "Authentication": {
      "Mode": "ApiKey",
      "ApiManagement": {
        "SubscriptionKey": "<from-key-vault>",
        "SubscriptionHeaderKey": "Ocp-Apim-Subscription-Key",
        "DefaultHeaders": {
          "x-routing-hint": "primary"
        }
      }
    }
  }
}
```

`ODataAuthenticationHandler` adds `SubscriptionHeaderKey: SubscriptionKey` to every request, then any entries in `DefaultHeaders` (routing hints, correlation IDs). `DefaultHeaders` is applied in `ApiKey` mode only.

> [!WARNING]
> `DefaultHeaders` must not carry an authentication header. `ODataSettingsValidator` rejects `Authorization`, `Bearer`, `Ocp-Apim-Subscription-Key`, or your configured `SubscriptionHeaderKey` there and fails startup — the framework owns the auth header.

## Send a command under either mode

The authentication mode is transparent to callers. The same `IMediator.Send` works regardless of mode, because the handler applies the header on the outbound HTTP call. This creates a `LedgerJournalHeader` in company `USMF`:

```csharp
public sealed class LedgerJournalHeader : BaseEntity
{
    [JsonPropertyName("dataAreaId")]
    public required string DataAreaId { get; set; }

    [ODataField(IgnoreOnCreate = true)]
    public string JournalBatchNumber { get; set; } = string.Empty;

    [ODataField(IgnoreOnUpdate = true)]
    public required string JournalName { get; set; }

    public required string Description { get; set; }

    public override object[] GetCompositeKey() => [DataAreaId, JournalBatchNumber];
}

Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(new LedgerJournalHeader
    {
        DataAreaId = "USMF",
        JournalName = "GenJrn",
        Description = "Month-end accrual",
    }),
    cancellationToken);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    // OAuth mode with a bad secret: the 401 short-circuit surfaces here as a failed Result —
    // Code "Auth.Msal.invalid_client", Message "Token acquisition failed".
    return;
}

// D365 assigns JournalBatchNumber server-side (IgnoreOnCreate).
string batch = result.Value.JournalBatchNumber!;
```

## Choose between OAuth and ApiKey

Both modes are shipped and supported. **Prefer OAuth for a service that calls D365 directly** — it owns its own identity, its token, and its D365 permissions.

- Choose **OAuth** when: your Function calls the D365 OData endpoint directly; you want the app's own Azure AD identity and per-app D365 permissions; there is no gateway in the path.
- Choose **ApiKey** when: Azure API Management fronts D365 and holds the OAuth credentials in its policy; one APIM instance routes across dev/test/prod by header; throttling and quota live at the gateway.

## Store secrets

Both `ClientSecret` and `SubscriptionKey` are secrets. Keep them out of source control.

| Environment | Store |
|---|---|
| Local development | `local.settings.json` (gitignored — never commit) |
| Deployed | Azure Key Vault, resolved via Managed Identity / `DefaultAzureCredential` |

The host wires Key Vault in `SampleFunction/Program.cs` with `DefaultAzureCredential` and the `Azure.Extensions.AspNetCore.Configuration.Secrets` provider — see [Set Up Azure Functions Host](Set-Up-Azure-Functions-Host). The framework never structured-logs the whole `ODataSettings`; only `Url` and `Mode` are visible at startup, never the credentials.

> [!CAUTION]
> Never log `ClientSecret`, `SubscriptionKey`, or an acquired bearer token — not in application logs, not in an HTTP response body, not in a `ReasonPhrase`.

## Switch mode per environment

The mode is read from `IOptions<ODataSettings>` on every request, so set it once at startup per environment via `ConfigureOData`:

```csharp
services.AddIntegratoR(configuration, integrator =>
{
    integrator.ConfigureOData(settings =>
    {
        settings.Authentication.Mode = context.HostingEnvironment.IsDevelopment()
            ? AuthenticationMode.OAuth   // direct in dev
            : AuthenticationMode.ApiKey; // APIM in deployed environments
    });
});
```

Per-request mode switching is not supported; the handler is wired once when the `HttpClient` is built.

## See Also

- [Configure OData](Configure-OData)
- [Set Up Azure Functions Host](Set-Up-Azure-Functions-Host)
- [Handle Errors](Handle-Errors)
- [Troubleshoot Common Issues](Troubleshoot-Common-Issues)
