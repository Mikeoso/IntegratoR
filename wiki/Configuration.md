# Configure OData

## Full Configuration Reference

```json
{
  "ODataSettings": {
    "Url": "https://your-environment.operations.dynamics.com/data",
    "Timeout": 120,
    "MetadataFilePath": "metadata.xml",
    "Authentication": {
      "Mode": "OAuth",
      "OAuth": {
        "ClientId": "your-azure-ad-app-id",
        "ClientSecret": "your-client-secret",
        "TenantId": "your-tenant-id",
        "Resource": "https://your-environment.operations.dynamics.com"
      },
      "ApiManagement": {
        "SubscriptionKey": "your-subscription-key",
        "SubscriptionHeaderKey": "Ocp-Apim-Subscription-Key",
        "DefaultHeaders": {
          "d365foenvironment": "DDI",
          "d365batchendpoint": "false"
        }
      }
    },
    "Resilience": {
      "EnableRetries": true,
      "RetryCount": 3,
      "UseCircuitBreaker": true,
      "CircuitBreakerThreshold": 5,
      "CircuitBreakerDurationInSeconds": 30
    }
  },
  "FOSettings": {
    "DimensionFormatName": "MainAccount-BusinessUnit-CostCenter",
    "DimensionHierarchyType": "DataEntityLedgerDimensionFormat"
  },
  "RelionSettings": {
    "Url": "https://relion-api.example.com",
    "Company": "USMF",
    "AuthMode": "OAuth",
    "ClientId": "your-azure-ad-app-id",
    "ClientSecret": "your-client-secret",
    "TenantId": "your-tenant-id",
    "Resource": "https://relion-api.example.com"
  }
}
```

Each section binds to a strongly-typed settings class via [[Getting-Started|DI registration]]:

```csharp
services.AddODataClient(configuration);        // binds ODataSettings
services.AddODataClientFOProxy(configuration); // binds FOSettings
services.AddRelionClient(configuration);       // binds RelionSettings
```

> **Azure App Settings / Environment Variables:** When deploying to Azure or Linux, replace `:` with `__` (double underscore) in key paths. For example, `ODataSettings:Authentication:OAuth:ClientId` becomes `ODataSettings__Authentication__OAuth__ClientId`.

## ODataSettings

Bound from the `"ODataSettings"` section. Controls the OData client connection to D365 F&O. Settings are grouped into sub-objects by concern: connection, authentication, and resilience.

**Note:** The `Url` must end with `/data` (e.g. `https://your-environment.operations.dynamics.com/data`).

### Root Properties

| Setting | Type | Required | Default | Description |
|---------|------|----------|---------|-------------|
| `Url` | `string` | **Yes** | `""` | Base URL of the D365 F&O OData endpoint |
| `Timeout` | `double` | No | `120` | HTTP request timeout in seconds |
| `MetadataFilePath` | `string?` | No | `null` | Path to a local `metadata.xml` file. Recommended for production to avoid DTD security issues and reduce startup time |

### Authentication

Nested under `"Authentication"`. Controls how the OData client authenticates with the endpoint.

| Setting | Type | Required | Default | Description |
|---------|------|----------|---------|-------------|
| `Mode` | `AuthenticationMode` | **Yes** | `ApiKey` | `ApiKey` (via APIM gateway) or `OAuth` (direct client credentials) |

#### OAuth 2.0 (when `Mode` is `OAuth`)

Nested under `"Authentication:OAuth"`. Required when using direct OAuth 2.0 client credentials flow.

| Setting | Type | Required | Default | Description |
|---------|------|----------|---------|-------------|
| `ClientId` | `string` | **Yes** | `""` | Azure AD Application (client) ID |
| `ClientSecret` | `string` | **Yes** | `""` | Azure AD client secret (store in Key Vault for production) |
| `TenantId` | `string` | **Yes** | `""` | Azure AD Directory (tenant) ID |
| `Resource` | `string` | **Yes** | `""` | App ID URI of the D365 F&O resource (typically the environment base URL) |

#### API Management (when `Mode` is `ApiKey`)

Nested under `"Authentication:ApiManagement"`. Required when routing through Azure API Management.

| Setting | Type | Required | Default | Description |
|---------|------|----------|---------|-------------|
| `SubscriptionKey` | `string` | **Yes** | `""` | Subscription key for the API gateway |
| `SubscriptionHeaderKey` | `string` | No | `Ocp-Apim-Subscription-Key` | HTTP header name for the subscription key |
| `DefaultHeaders` | `Dictionary<string, string>` | No | `{}` | Additional HTTP headers sent with every APIM request |

### Resilience

Nested under `"Resilience"`. Controls retry and circuit breaker policies.

| Setting | Type | Required | Default | Description |
|---------|------|----------|---------|-------------|
| `EnableRetries` | `bool` | No | `true` | Enable automatic retry with exponential backoff (429, 5xx) |
| `RetryCount` | `int` | No | `3` | Number of retry attempts (1-10) |
| `UseCircuitBreaker` | `bool` | No | `true` | Enable circuit breaker to prevent cascading failures |
| `CircuitBreakerThreshold` | `int` | No | `5` | Consecutive failures before the circuit opens |
| `CircuitBreakerDurationInSeconds` | `int` | No | `30` | Seconds the circuit stays open before recovery |

## Scenario Examples

### Direct OAuth to D365 F&O

```json
{
  "ODataSettings": {
    "Url": "https://your-environment.operations.dynamics.com/data",
    "Authentication": {
      "Mode": "OAuth",
      "OAuth": {
        "ClientId": "your-azure-ad-app-id",
        "ClientSecret": "your-client-secret",
        "TenantId": "your-tenant-id",
        "Resource": "https://your-environment.operations.dynamics.com"
      }
    }
  }
}
```

### Via Azure API Management Gateway

```json
{
  "ODataSettings": {
    "Url": "https://your-apim.azure-api.net/d365",
    "Authentication": {
      "Mode": "ApiKey",
      "ApiManagement": {
        "SubscriptionKey": "your-subscription-key"
      }
    }
  }
}
```

### Disabling Resilience (Testing)

```json
{
  "ODataSettings": {
    "Url": "https://localhost:5000/data",
    "Authentication": {
      "Mode": "ApiKey",
      "ApiManagement": {
        "SubscriptionKey": "test-key"
      }
    },
    "Resilience": {
      "EnableRetries": false,
      "UseCircuitBreaker": false
    }
  }
}
```

### Programmatic Configuration

```csharp
services.AddODataClient(options =>
{
    options.Url = "https://your-environment.operations.dynamics.com/data";
    options.Authentication.Mode = AuthenticationMode.OAuth;
    options.Authentication.OAuth.ClientId = Environment.GetEnvironmentVariable("D365_CLIENT_ID")!;
    options.Authentication.OAuth.ClientSecret = Environment.GetEnvironmentVariable("D365_CLIENT_SECRET")!;
    options.Authentication.OAuth.TenantId = Environment.GetEnvironmentVariable("D365_TENANT_ID")!;
    options.Authentication.OAuth.Resource = "https://your-environment.operations.dynamics.com";
});
```

## FOSettings

Bound from the `"FOSettings"` section. Controls D365 F&O financial dimension behaviour.

| Setting | Type | Required | Default | Description |
|---------|------|----------|---------|-------------|
| `DimensionFormatName` | `string` | **Yes** | `""` | Financial dimension format name from D365 F&O setup (**General ledger > Chart of accounts > Dimensions > Financial dimension formats**) |
| `DimensionHierarchyType` | `DimensionHierarchyType` | No | `AccountStructure` | Type of dimension hierarchy |

### DimensionHierarchyType Values

| Value | Description |
|-------|-------------|
| `AccountStructure` | Primary chart of accounts structure |
| `DataEntityDefaultDimensionFormat` | Default dimension format (without main account) for data entities |
| `DataEntityLedgerDimensionFormat` | Ledger dimension format (main account + dimensions) for data entities |
| `DataEntityBudgetDimensionFormat` | Budget dimension format for data entities |
| `Focus` | Budgeting and planning structure |
| `Customer` | Dimensions linked to customer master records |
| `Vendor` | Dimensions linked to vendor master records |

## RelionSettings

Bound from the `"RelionSettings"` section. Controls the RELion API connection.

### Connection

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Url` | `string` | *(required)* | Base URL of the RELion API endpoint |
| `Timeout` | `int` | `120` | HTTP request timeout in seconds |
| `Company` | `string` | `""` | Company identifier for API requests |
| `AuthMode` | `RelionAuthMode` | `ApiKey` | `ApiKey` or `OAuth` |

### OAuth 2.0 (when `AuthMode` is `OAuth`)

| Setting | Type | Description |
|---------|------|-------------|
| `ClientId` | `string` | Azure AD Application (client) ID |
| `ClientSecret` | `string` | Azure AD client secret |
| `TenantId` | `string` | Azure AD Directory (tenant) ID |
| `Resource` | `string` | App ID URI of the RELion resource |

### API Gateway (when `AuthMode` is `ApiKey`)

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `SubscriptionKey` | `string` | `""` | Subscription key for the API gateway |
| `SubscriptionHeaderKey` | `string` | `""` | HTTP header name for the subscription key |

## Authentication Modes

`ODataSettings` uses `AuthenticationMode` and `RelionSettings` uses `RelionAuthMode`. Both support the same two values:

| Mode | Protocol | Use when |
|------|----------|----------|
| `OAuth` | OAuth 2.0 client credentials flow (Bearer token) | Direct access to D365 F&O or RELion |
| `ApiKey` | Static subscription key in HTTP header | Access via Azure API Management gateway |

**OAuth** acquires a Bearer token from Azure AD automatically (acquire, cache, refresh) and adds it to the `Authorization` header. Requires an Azure AD App Registration with a client secret and API permissions for the target service.

**ApiKey** adds a subscription key to the configured HTTP header. The API gateway handles backend authentication. Use this when D365 F&O or RELion is fronted by Azure API Management.

## See Also

- [[Getting-Started]] — minimal configuration for first setup
- [[Resilience]] — retry and circuit breaker behaviour details
- [[Azure-Functions-Host]] — production configuration with Key Vault
- [[RELion]] — RELion-specific connection setup
