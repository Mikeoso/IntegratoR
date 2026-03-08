# ODataSettings

Configuration class for the OData client connection, authentication, retry policies, and circuit breaker. Bound from the `"ODataSettings"` section in `appsettings.json`.

## Configure the Settings

```json
{
  "ODataSettings": {
    "Url": "https://your-environment.operations.dynamics.com/data",
    "AuthMode": "OAuth",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "TenantId": "your-tenant-id",
    "Resource": "https://your-environment.operations.dynamics.com"
  }
}
```

```csharp
services.AddODataClient(configuration);
// Settings are available via IOptions<ODataSettings>
```

## Settings

### General Connection

| Setting | Type | Required | Default | Description |
|---------|------|----------|---------|-------------|
| `Url` | `string` | Yes | `""` | Base URL of the D365 F&O OData endpoint (e.g. `https://env.operations.dynamics.com/data`) |
| `Timeout` | `double` | No | `120` | HTTP request timeout in seconds |
| `AuthMode` | `ODataAuthMode` | Yes | `ApiKey` | Authentication method: `ApiKey` or `OAuth` |
| `DefaultHeaders` | `Dictionary<string, string>` | No | `{}` | Additional HTTP headers sent with every request |

### OAuth 2.0 (Client Credentials)

Required when `AuthMode` is `OAuth`.

| Setting | Type | Required | Default | Description |
|---------|------|----------|---------|-------------|
| `ClientId` | `string` | Yes* | `""` | Azure AD app registration Application ID |
| `ClientSecret` | `string` | Yes* | `""` | Azure AD app registration secret (store in Key Vault) |
| `TenantId` | `string` | Yes* | `""` | Azure AD Directory (tenant) ID |
| `Resource` | `string` | Yes* | `""` | App ID URI of the D365 F&O resource (typically the environment base URL) |

### API Gateway (API Key)

Required when `AuthMode` is `ApiKey`.

| Setting | Type | Required | Default | Description |
|---------|------|----------|---------|-------------|
| `SubscriptionKey` | `string` | Yes* | `""` | Subscription key for the API gateway |
| `SubscriptionHeaderKey` | `string` | No | `"Ocp-Apim-Subscription-Key"` | HTTP header name for the subscription key |

### Retry and Resilience

| Setting | Type | Required | Default | Description |
|---------|------|----------|---------|-------------|
| `EnableRetries` | `bool` | No | `true` | Enable automatic retry on transient failures |
| `RetryCount` | `int` | No | `3` | Number of retry attempts (valid range: 1-10) |
| `UseCircuitBreaker` | `bool` | No | `true` | Enable circuit breaker pattern |
| `CircuitBreakerThreshold` | `int` | No | `5` | Consecutive failures before the circuit opens |
| `CircuitBreakerDurationSeconds` | `int` | No | `30` | Seconds the circuit stays open before recovery |

### Metadata

| Setting | Type | Required | Default | Description |
|---------|------|----------|---------|-------------|
| `MetadataFilePath` | `string?` | No | `null` | Path to a local `metadata.xml` file. Recommended for production. |

## See Examples

### Full OAuth Configuration

```json
{
  "ODataSettings": {
    "Url": "https://your-environment.operations.dynamics.com/data",
    "Timeout": 120,
    "AuthMode": "OAuth",
    "ClientId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
    "ClientSecret": "your-secret-value",
    "TenantId": "ffffffff-gggg-hhhh-iiii-jjjjjjjjjjjj",
    "Resource": "https://your-environment.operations.dynamics.com",
    "EnableRetries": true,
    "RetryCount": 3,
    "UseCircuitBreaker": true,
    "CircuitBreakerThreshold": 5,
    "CircuitBreakerDurationSeconds": 30,
    "MetadataFilePath": "metadata.xml"
  }
}
```

### API Gateway Configuration

```json
{
  "ODataSettings": {
    "Url": "https://your-apim-gateway.azure-api.net/d365",
    "AuthMode": "ApiKey",
    "SubscriptionKey": "your-subscription-key",
    "SubscriptionHeaderKey": "Ocp-Apim-Subscription-Key",
    "EnableRetries": true,
    "RetryCount": 5
  }
}
```

### Programmatic Configuration

```csharp
services.AddODataClient(options =>
{
    options.Url = "https://your-environment.operations.dynamics.com/data";
    options.AuthMode = ODataAuthMode.OAuth;
    options.ClientId = Environment.GetEnvironmentVariable("D365_CLIENT_ID")!;
    options.ClientSecret = Environment.GetEnvironmentVariable("D365_CLIENT_SECRET")!;
    options.TenantId = Environment.GetEnvironmentVariable("D365_TENANT_ID")!;
    options.Resource = "https://your-environment.operations.dynamics.com";
    options.EnableRetries = true;
    options.RetryCount = 3;
});
```

### Disabling Resilience (Testing)

```json
{
  "ODataSettings": {
    "Url": "https://localhost:5000/data",
    "AuthMode": "ApiKey",
    "SubscriptionKey": "test-key",
    "EnableRetries": false,
    "UseCircuitBreaker": false
  }
}
```

### Error Handling

Invalid or missing settings cause failures at runtime when the OData client attempts to connect:

```csharp
// Missing Url -> HttpClient throws on first request
// Missing ClientId with OAuth -> authentication handler fails
// Result will contain IntegrationError with ErrorType.Failure
```

## ODataAuthMode Enum

| Value | Description |
|-------|-------------|
| `ApiKey` | Static API/subscription key via HTTP header. Used with API gateways (e.g. Azure APIM). |
| `OAuth` | OAuth 2.0 client credentials flow. Recommended for direct D365 F&O access. |

## See Also

- [[API-AddODataClient]] — DI registration that binds these settings
- [[Authentication-Modes]] — when to use OAuth vs ApiKey
- [[Configure-the-OData-Connection]] — step-by-step setup guide
