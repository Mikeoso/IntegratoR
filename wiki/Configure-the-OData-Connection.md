# Configure the OData Connection

Before sending commands, you need to tell the OData client where your D365 F&O environment lives and how to authenticate. This page assumes you have already [[Install-the-Framework|installed the framework]].

## Add the Configuration Section

Add an `ODataSettings` section to your `appsettings.json`:

```json
{
  "ODataSettings": {
    "Url": "https://your-environment.operations.dynamics.com/data",
    "AuthMode": "OAuth",
    "ClientId": "your-azure-ad-app-id",
    "ClientSecret": "your-client-secret",
    "TenantId": "your-tenant-id",
    "Resource": "https://your-environment.operations.dynamics.com",
    "Timeout": 120,
    "EnableRetries": true,
    "RetryCount": 3,
    "UseCircuitBreaker": true,
    "CircuitBreakerThreshold": 5,
    "CircuitBreakerDurationSeconds": 30
  }
}
```

## Configuration Reference

### General Connection

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Url` | `string` | *(required)* | Base URL of the D365 F&O OData endpoint (e.g. `https://your-environment.operations.dynamics.com/data`) |
| `Timeout` | `double` | `120` | HTTP request timeout in seconds |
| `AuthMode` | `ODataAuthMode` | `ApiKey` | Authentication method: `ApiKey` or `OAuth` |
| `DefaultHeaders` | `Dictionary<string,string>` | `{}` | Additional HTTP headers sent with every request |

### OAuth 2.0 (Client Credentials)

| Property | Type | Description |
|----------|------|-------------|
| `ClientId` | `string` | Azure AD Application (client) ID |
| `ClientSecret` | `string` | Azure AD client secret -- store in Key Vault, not in plain text |
| `TenantId` | `string` | Azure AD Directory (tenant) ID |
| `Resource` | `string` | App ID URI of the D365 F&O resource (typically the environment base URL) |

### API Gateway (Azure API Management)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SubscriptionKey` | `string` | | Subscription key for the API gateway |
| `SubscriptionHeaderKey` | `string` | `Ocp-Apim-Subscription-Key` | HTTP header name for the subscription key |

### Retry and Resilience

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableRetries` | `bool` | `true` | Enable automatic retries with exponential backoff for transient failures (429, 5xx) |
| `RetryCount` | `int` | `3` | Number of retry attempts |
| `UseCircuitBreaker` | `bool` | `true` | Enable circuit breaker to prevent cascading failures |
| `CircuitBreakerThreshold` | `int` | `5` | Consecutive failures before the circuit opens |
| `CircuitBreakerDurationSeconds` | `int` | `30` | Seconds the circuit stays open before allowing a test request |

### Metadata

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MetadataFilePath` | `string?` | `null` | Path to a local `metadata.xml` file. Recommended for production to avoid DTD security issues and reduce startup time |

## Minimal OAuth Configuration

If you only need OAuth with defaults for resilience, the minimal configuration is:

```json
{
  "ODataSettings": {
    "Url": "https://your-environment.operations.dynamics.com/data",
    "AuthMode": "OAuth",
    "ClientId": "...",
    "ClientSecret": "...",
    "TenantId": "...",
    "Resource": "https://your-environment.operations.dynamics.com"
  }
}
```

Retries (3 attempts) and circuit breaker (threshold 5, 30s duration) are enabled by default.

## Common Mistakes

**Missing or wrong `Url`** -- The `Url` must end with `/data`. A missing suffix results in 404 responses from D365 F&O.

```
// Wrong
"Url": "https://your-environment.operations.dynamics.com"

// Correct
"Url": "https://your-environment.operations.dynamics.com/data"
```

## What Just Happened

- You added an `ODataSettings` configuration section that the OData client reads at startup.
- The client will authenticate using OAuth 2.0 client credentials and apply retry/circuit breaker policies automatically.
- Secrets like `ClientSecret` should be stored in Azure Key Vault and referenced via configuration providers, not committed to source control.

## See Also

- [[Register-Services-in-Your-Host]] — register OData services after configuring settings
- [[Install-the-Framework]] — install NuGet packages before configuration
- [[Send-Your-First-Command]] — use the configured connection to send a command
