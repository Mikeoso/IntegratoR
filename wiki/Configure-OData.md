# Configure OData

All HTTP communication with D365 F&O is governed by the `ODataSettings` configuration section. The structure is nested — connection settings at the root, authentication mode and credentials under `Authentication`, retry and circuit breaker policies under `Resilience`.

```json
{
  "ODataSettings": {
    "Url": "https://your-environment.operations.dynamics.com/data",
    "Timeout": 120,
    "Authentication": {
      "Mode": "OAuth",
      "OAuth": {
        "ClientId": "<azure-ad-app-id>",
        "ClientSecret": "<client-secret>",
        "TenantId": "<azure-ad-tenant-id>",
        "Resource": "https://your-environment.operations.dynamics.com"
      }
    },
    "Resilience": {
      "EnableRetries": true,
      "RetryCount": 3,
      "UseCircuitBreaker": true,
      "CircuitBreakerThreshold": 5,
      "CircuitBreakerDurationInSeconds": 30
    }
  }
}
```

The framework auto-binds this section from `IConfiguration` when `AddIntegratoR(configuration)` runs. Programmatic overrides are also supported — see [Programmatic Overrides](#programmatic-overrides) below.

## Top-Level Settings

| Property | Type | Default | Purpose |
|---|---|---|---|
| `Url` | `string` | `""` | Base URL of the D365 F&O OData endpoint. Throws at startup if missing. Must include the path segment (typically `/data`). |
| `Timeout` | `double` | `120` | HTTP request timeout in seconds. Applied to `HttpClient.Timeout`. |
| `MetadataFilePath` | `string?` | `null` | Local path to a `metadata.xml` file. When set, the OData client uses this file instead of fetching the metadata document from the server. |
| `Authentication` | `ODataAuthenticationSettings` | `new()` | Nested authentication settings — see below. |
| `Resilience` | `ODataResilienceSettings` | `new()` | Nested retry and circuit breaker settings — see below. |

> The `Url` value is normalised by the framework to end with `/` so that the path segment (e.g. `/data` or `/fo`) is preserved when relative request URIs are appended. Both `https://host/data` and `https://host/data/` work — the framework writes back the normalised form into the bound options so PanoramicData's URI composition stays in lockstep with `HttpClient.BaseAddress`.

## Authentication Settings

The `Authentication` section is split across the `Mode` selector and two mode-specific sub-objects. Only the sub-object matching the selected `Mode` is read.

| Property | Type | Default | Purpose |
|---|---|---|---|
| `Authentication.Mode` | `AuthenticationMode` | `ApiKey` | Either `OAuth` (direct client credentials to D365) or `ApiKey` (via Azure API Management). |
| `Authentication.OAuth.ClientId` | `string` | `""` | Application (client) ID from the Azure AD app registration. |
| `Authentication.OAuth.ClientSecret` | `string` | `""` | Client secret from the same app registration. Source from Key Vault in production. |
| `Authentication.OAuth.TenantId` | `string` | `""` | Azure AD tenant ID where the app is registered. |
| `Authentication.OAuth.Resource` | `string` | `""` | App ID URI of the target D365 environment (usually the environment URL **without** `/data`). |
| `Authentication.ApiManagement.SubscriptionKey` | `string` | `""` | APIM subscription key. |
| `Authentication.ApiManagement.SubscriptionHeaderKey` | `string` | `"Ocp-Apim-Subscription-Key"` | Name of the HTTP header that carries the subscription key. |
| `Authentication.ApiManagement.DefaultHeaders` | `Dictionary<string,string>` | `{}` | Extra static headers added to every outbound request through APIM (e.g. routing hints). |

The default `Mode` is `ApiKey` to match the APIM-fronted production topology. Direct D365 calls require setting `Mode: "OAuth"` explicitly. See [Authentication Modes](Authentication-Modes) for an end-to-end walkthrough of each mode and the Azure AD configuration required.

## Resilience Settings

| Property | Type | Default | Purpose |
|---|---|---|---|
| `Resilience.EnableRetries` | `bool` | `true` | Master switch for the Polly retry policy. |
| `Resilience.RetryCount` | `int` | `3` | Number of retry attempts after the initial request. Valid range 1–10. |
| `Resilience.UseCircuitBreaker` | `bool` | `true` | Master switch for the Polly circuit breaker. |
| `Resilience.CircuitBreakerThreshold` | `int` | `5` | Consecutive transient failures before the circuit opens. |
| `Resilience.CircuitBreakerDurationInSeconds` | `int` | `30` | Seconds the circuit stays open before transitioning to half-open. |

Retry and circuit breaker are independent — disabling retries does not disable the breaker, and vice versa. See [Configure Resilience](Configure-Resilience) for what gets retried, the backoff/jitter formula, and circuit breaker state transitions.

## Azure App Settings Format

When the configuration source is Azure App Settings (or any environment-variable-based provider), nested JSON keys are expressed with the double-underscore separator:

```
ODataSettings__Url                                          = https://...
ODataSettings__Authentication__Mode                         = OAuth
ODataSettings__Authentication__OAuth__ClientId              = ...
ODataSettings__Authentication__OAuth__ClientSecret          = ...
ODataSettings__Authentication__OAuth__TenantId              = ...
ODataSettings__Authentication__OAuth__Resource              = https://...
ODataSettings__Resilience__RetryCount                       = 3
```

Local `local.settings.json` uses the standard JSON nesting shown at the top of this page. Mixed setups (defaults in JSON, overrides via App Settings) work transparently — last value wins.

## Programmatic Overrides

The `IntegratoRBuilder` exposes a `ConfigureOData` hook for code-driven overrides. The delegate runs as a `PostConfigure` callback **after** the JSON binding, so it overrides anything from `appsettings.json`:

```csharp
services.AddIntegratoR(configuration, integrator =>
{
    integrator.ConfigureOData(settings =>
    {
        settings.Timeout = 180;
        settings.Resilience.RetryCount = 5;
        settings.Resilience.UseCircuitBreaker = false;
    });
});
```

Multiple `ConfigureOData` calls are composed in registration order. Each delegate sees the cumulative result of the previous calls.

The same hook accepts the entire settings object — useful for environment-conditional configuration:

```csharp
integrator.ConfigureOData(settings =>
{
    if (context.HostingEnvironment.IsDevelopment())
    {
        settings.Resilience.UseCircuitBreaker = false;   // never break a dev sandbox
    }
});
```

## Local Metadata File

D365 F&O serves the full CSDL metadata document at `<Url>/$metadata`. The document is large (tens of megabytes) and sometimes triggers DTD parsing failures in PanoramicData.OData.Client. Pointing `MetadataFilePath` at a local snapshot avoids both problems:

```json
{
  "ODataSettings": {
    "MetadataFilePath": "metadata.xml"
  }
}
```

Trade-offs: faster startup, offline development, traceable changes through source control — at the cost of manual refreshes when the D365 metadata evolves. Download with:

```bash
curl -H "Authorization: Bearer $TOKEN" \
     https://your-environment.operations.dynamics.com/data/\$metadata \
     -o metadata.xml
```

## Verify the Configuration

The fastest way to confirm that the settings are correct is to run the bundled smoke test triggers — see [Run Smoke Tests](Run-Smoke-Tests). Either trigger surfaces authentication, network, and APIM errors as `IntegrationError` payloads in the JSON response, so misconfiguration is visible without reading logs.

## When Things Go Wrong

**`ArgumentException` at startup mentioning `ODataSettings.Url`:** the URL is missing, empty, or whitespace. The framework refuses to start with an unset URL to prevent the cryptic `UriFormatException` that would otherwise surface at the first HTTP call.

**HTTP 404 with the path segment missing from the request URL:** the URL was supplied without a trailing slash AND without the framework's normalisation. Verify the URL includes `/data` (or `/fo` for an APIM rewrite) and check the host logs — the framework logs the normalised `BaseAddress` once at startup.

**OAuth token acquisition fails with `AADSTS70011`:** the `Resource` value is wrong. For most D365 environments the resource is the environment URL **without** `/data`, not the full OData endpoint.

**Circuit breaker keeps tripping in development:** lower `Resilience.CircuitBreakerThreshold` to a high number or set `UseCircuitBreaker: false` for the development environment. Sandbox D365 environments occasionally return spurious 5xx that look transient but are actually slow cold-starts.

## See Also

- [Authentication Modes](Authentication-Modes) — OAuth vs API Key walkthrough
- [Configure Resilience](Configure-Resilience) — what gets retried, backoff formula, circuit breaker mechanics
- [Run Smoke Tests](Run-Smoke-Tests) — verify the configuration end-to-end
- [Troubleshoot Common Issues](Troubleshoot-Common-Issues) — common configuration errors and resolutions
