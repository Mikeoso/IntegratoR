# Configuration

```json
{
  "ODataSettings": {
    "Url": "https://your-environment.operations.dynamics.com/data",
    "AuthMode": "OAuth",
    "ClientId": "your-azure-ad-app-id",
    "ClientSecret": "your-client-secret",
    "TenantId": "your-tenant-id",
    "Resource": "https://your-environment.operations.dynamics.com"
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

## ODataSettings

Bound from the `"ODataSettings"` section. Controls the OData client connection to D365 F&O.

**Note:** The `Url` must end with `/data` (e.g. `https://your-environment.operations.dynamics.com/data`).

### Connection

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Url` | `string` | *(required)* | Base URL of the D365 F&O OData endpoint |
| `Timeout` | `double` | `120` | HTTP request timeout in seconds |
| `AuthMode` | `ODataAuthMode` | `ApiKey` | `ApiKey` or `OAuth` |
| `DefaultHeaders` | `Dictionary<string, string>` | `{}` | Additional HTTP headers sent with every request |

### OAuth 2.0 (when `AuthMode` is `OAuth`)

| Setting | Type | Description |
|---------|------|-------------|
| `ClientId` | `string` | Azure AD Application (client) ID |
| `ClientSecret` | `string` | Azure AD client secret (store in Key Vault) |
| `TenantId` | `string` | Azure AD Directory (tenant) ID |
| `Resource` | `string` | App ID URI of the D365 F&O resource (typically the environment base URL) |

### API Gateway (when `AuthMode` is `ApiKey`)

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `SubscriptionKey` | `string` | `""` | Subscription key for the API gateway |
| `SubscriptionHeaderKey` | `string` | `Ocp-Apim-Subscription-Key` | HTTP header name for the subscription key |

### Retry and Resilience

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `EnableRetries` | `bool` | `true` | Enable automatic retry with exponential backoff (429, 5xx) |
| `RetryCount` | `int` | `3` | Number of retry attempts (1-10) |
| `UseCircuitBreaker` | `bool` | `true` | Enable circuit breaker to prevent cascading failures |
| `CircuitBreakerThreshold` | `int` | `5` | Consecutive failures before the circuit opens |
| `CircuitBreakerDurationSeconds` | `int` | `30` | Seconds the circuit stays open before recovery |

### Metadata

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `MetadataFilePath` | `string?` | `null` | Path to a local `metadata.xml` file. Recommended for production to avoid DTD security issues and reduce startup time |

## FOSettings

Bound from the `"FOSettings"` section. Controls D365 F&O financial dimension behaviour.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `DimensionFormatName` | `string` | *(required)* | Financial dimension format name from D365 F&O setup (**General ledger > Chart of accounts > Dimensions > Financial dimension formats**) |
| `DimensionHierarchyType` | `DimensionHierarchyType` | `AccountStructure` | Type of dimension hierarchy |

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

Both `ODataAuthMode` and `RelionAuthMode` support the same two values:

| Mode | Protocol | Use when |
|------|----------|----------|
| `OAuth` | OAuth 2.0 client credentials flow (Bearer token) | Direct access to D365 F&O or RELion |
| `ApiKey` | Static subscription key in HTTP header | Access via Azure API Management gateway |

**OAuth** acquires a Bearer token from Azure AD automatically (acquire, cache, refresh) and adds it to the `Authorization` header. Requires an Azure AD App Registration with a client secret and API permissions for the target service.

**ApiKey** adds a subscription key to the configured HTTP header. The API gateway handles backend authentication. Use this when D365 F&O or RELion is fronted by Azure API Management.

### OAuth via API Gateway

```json
{
  "ODataSettings": {
    "Url": "https://your-apim.azure-api.net/d365",
    "AuthMode": "ApiKey",
    "SubscriptionKey": "your-subscription-key",
    "SubscriptionHeaderKey": "Ocp-Apim-Subscription-Key"
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
