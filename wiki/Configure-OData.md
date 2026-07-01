# Configure OData
> Last verified against v2.0.1

`ODataSettings` governs every HTTP call to D365 F&O — the endpoint URL, the authentication mode and its credentials, and the retry/circuit-breaker policies. Bind the `"ODataSettings"` section and wire it with `AddIntegratoR`; the framework validates it at startup.

```json
{
  "ODataSettings": {
    "Url": "https://your-environment.operations.dynamics.com/data",
    "Timeout": 120,
    "Authentication": {
      "Mode": "OAuth",
      "OAuth": {
        "ClientId": "<azure-ad-app-id>",
        "ClientSecret": "<from-key-vault>",
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

```csharp
// Program.cs — the one line that binds and wires everything.
services.AddIntegratoR(configuration);
```

`AddIntegratoR` reads the `"ODataSettings"` section from `IConfiguration`, registers the OData client, and installs the settings validator. To override any value in code, pass the builder delegate and call `ConfigureOData` — it runs as a `PostConfigure` after JSON binding, so it wins:

```csharp
services.AddIntegratoR(configuration, integrator =>
{
    integrator.ConfigureOData(settings =>
    {
        settings.Timeout = 180;
        settings.Resilience.RetryCount = 5;
    });
});
```

## Top-level options

| Property | Type | Default | Purpose |
|---|---|---|---|
| `Url` | `string` | `""` | Base URL of the D365 F&O OData endpoint, including the `/data` path segment. The framework normalises it to end with `/` so relative request URIs preserve the path; it throws `ArgumentException` at DI resolution if the value is null or whitespace. |
| `Timeout` | `double` | `120` | Per-request HTTP timeout in seconds, applied to `HttpClient.Timeout`. |
| `MetadataFilePath` | `string?` | `null` | Local path to a `metadata.xml` snapshot. When set, the client reads it instead of fetching CSDL from the server. |
| `Authentication` | `ODataAuthenticationSettings` | `new()` | Nested authentication settings — see below. |
| `Resilience` | `ODataResilienceSettings` | `new()` | Nested retry and circuit-breaker settings — see below. |

## Authentication options

The `Authentication.Mode` selector picks one of two mutually exclusive credential blocks. Only the block matching the selected mode is read; the validator checks that block is populated.

| Property | Type | Default | Purpose |
|---|---|---|---|
| `Authentication.Mode` | `AuthenticationMode` | `ApiKey` | `ApiKey` (subscription key via Azure API Management) or `OAuth` (direct client credentials to D365). |
| `Authentication.OAuth.ClientId` | `string` | `""` | Application (client) ID from the Azure AD app registration. |
| `Authentication.OAuth.ClientSecret` | `string` | `""` | Client secret for the service principal. Source it from Azure Key Vault in production. |
| `Authentication.OAuth.TenantId` | `string` | `""` | Azure AD tenant ID where the app is registered. |
| `Authentication.OAuth.Resource` | `string` | `""` | App ID URI of the target D365 environment (usually the environment URL **without** `/data`). |
| `Authentication.ApiManagement.SubscriptionKey` | `string` | `""` | APIM subscription key. |
| `Authentication.ApiManagement.SubscriptionHeaderKey` | `string` | `"Ocp-Apim-Subscription-Key"` | Name of the HTTP header that carries the subscription key. |
| `Authentication.ApiManagement.DefaultHeaders` | `Dictionary<string,string>` | `{}` | Extra static headers sent on every APIM request (e.g. routing hints). |

The default mode is `ApiKey` to match an APIM-fronted topology. Set `"Mode": "OAuth"` explicitly for direct D365 calls. See [Authentication Modes](Authentication-Modes) for the end-to-end walkthrough of each mode and its Azure AD setup.

> [!WARNING]
> Never put `Authorization`, `Bearer`, `Ocp-Apim-Subscription-Key`, or your configured `SubscriptionHeaderKey` into `DefaultHeaders`. The framework owns the auth header; a smuggled one fails startup validation (see below).

## Resilience options

| Property | Type | Default | Purpose |
|---|---|---|---|
| `Resilience.EnableRetries` | `bool` | `true` | Master switch for the Polly retry policy. |
| `Resilience.RetryCount` | `int` | `3` | Retry attempts after the initial request. The 1–10 range is a documented recommendation, not enforced by the binder — a value outside it is accepted as written. |
| `Resilience.UseCircuitBreaker` | `bool` | `true` | Master switch for the Polly circuit breaker. |
| `Resilience.CircuitBreakerThreshold` | `int` | `5` | Consecutive transient failures before the circuit opens. |
| `Resilience.CircuitBreakerDurationInSeconds` | `int` | `30` | Seconds the circuit stays open before moving to half-open. |

Retry and circuit breaker toggle independently — disabling one leaves the other running. See [Configure Resilience](Configure-Resilience) for what gets retried, the backoff-with-jitter formula, and the circuit-breaker state machine.

## Fail-fast validation

`ODataSettingsValidator` implements `IValidateOptions<ODataSettings>` and is registered with `ValidateOnStart()`, so bad configuration surfaces at host startup — not at the first HTTP call. Materialising `IOptions<ODataSettings>.Value` throws `OptionsValidationException` when:

- `DefaultHeaders` carries an authentication header (compared case-insensitively).
- Mode is `ApiKey` but `SubscriptionKey` or `SubscriptionHeaderKey` is blank.
- Mode is `OAuth` but any of `ClientId`, `ClientSecret`, `TenantId`, `Resource` is blank.
- `Mode` holds an unrecognised value (e.g. a typo in config binding).

```csharp
// Startup fails fast with the accumulated reasons — for example:
// OptionsValidationException:
//   ODataSettings.Authentication.OAuth.ClientSecret must be set when AuthenticationMode is OAuth.
//   ODataSettings.Authentication.OAuth.TenantId must be set when AuthenticationMode is OAuth.
```

A blank `Url` is caught separately: `NormaliseBaseUrl` throws `ArgumentException` when the OData client is resolved, with a message pointing at your configuration.

## JSON key separators

Local `local.settings.json` uses standard JSON nesting (the block at the top of this page). On Azure App Settings — or any environment-variable provider — express the same keys with the double-underscore (`__`) separator:

```
ODataSettings__Url                             = https://...
ODataSettings__Authentication__Mode            = OAuth
ODataSettings__Authentication__OAuth__ClientId = ...
ODataSettings__Resilience__RetryCount          = 3
```

Mixed sources compose: JSON defaults with App Settings overrides, last value wins.

## Local metadata snapshot

D365 F&O serves its full CSDL at `<Url>/$metadata`. The document runs to tens of megabytes and can trip DTD parsing in PanoramicData.OData.Client. Point `MetadataFilePath` at a local snapshot to skip the fetch:

```json
{ "ODataSettings": { "MetadataFilePath": "metadata.xml" } }
```

The trade-off is a manual refresh whenever the D365 metadata changes.

## See Also

- [Authentication Modes](Authentication-Modes) — OAuth versus API Key, end to end
- [Configure Resilience](Configure-Resilience) — retry set, backoff formula, circuit-breaker mechanics
- [Set Up Azure Functions Host](Set-Up-Azure-Functions-Host) — where `AddIntegratoR` and configuration are wired
- [Troubleshoot Common Issues](Troubleshoot-Common-Issues) — startup and connection errors
