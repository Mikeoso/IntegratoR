# RelionSettings

Configuration class for the RELion API connection, authentication, and company targeting. Bound from the `"RelionSettings"` section in `appsettings.json`.

## Configure the Settings

```json
{
  "RelionSettings": {
    "Url": "https://your-relion-instance.com",
    "Company": "USMF",
    "AuthMode": "OAuth",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "TenantId": "your-tenant-id",
    "Resource": "https://your-relion-instance.com"
  }
}
```

```csharp
services.AddRelionClient(configuration);
// Settings are available via IOptions<RelionSettings>
```

## Settings

### Base Settings

| Setting | Type | Required | Default | Description |
|---------|------|----------|---------|-------------|
| `Url` | `string` | Yes | -- | Base URL of the RELion API endpoint |
| `Timeout` | `int` | No | `120` | HTTP request timeout in seconds |
| `Company` | `string` | No | `""` | Company identifier within RELion for API requests |

### API Gateway (API Key)

Required when `AuthMode` is `ApiKey`.

| Setting | Type | Required | Default | Description |
|---------|------|----------|---------|-------------|
| `AuthMode` | `RelionAuthMode` | Yes | `ApiKey` | Authentication method: `ApiKey` or `OAuth` |
| `SubscriptionKey` | `string` | Yes* | `""` | Subscription key for the API gateway |
| `SubscriptionHeaderKey` | `string` | No | `""` | HTTP header name for the subscription key |

### OAuth 2.0 (Client Credentials)

Required when `AuthMode` is `OAuth`.

| Setting | Type | Required | Default | Description |
|---------|------|----------|---------|-------------|
| `ClientId` | `string` | Yes* | `""` | Azure AD app registration Application ID |
| `ClientSecret` | `string` | Yes* | `""` | Azure AD app registration secret |
| `TenantId` | `string` | Yes* | `""` | Azure AD Directory (tenant) ID |
| `Resource` | `string` | Yes* | `""` | App ID URI of the RELion resource |

## See Examples

### Full OAuth Configuration

```json
{
  "RelionSettings": {
    "Url": "https://relion-api.example.com",
    "Timeout": 120,
    "Company": "USMF",
    "AuthMode": "OAuth",
    "ClientId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
    "ClientSecret": "your-secret-value",
    "TenantId": "ffffffff-gggg-hhhh-iiii-jjjjjjjjjjjj",
    "Resource": "https://relion-api.example.com"
  }
}
```

### API Key Configuration

```json
{
  "RelionSettings": {
    "Url": "https://relion-apim.azure-api.net",
    "Company": "USMF",
    "AuthMode": "ApiKey",
    "SubscriptionKey": "your-subscription-key",
    "SubscriptionHeaderKey": "Ocp-Apim-Subscription-Key"
  }
}
```

### Error Handling

Missing or invalid settings cause failures at runtime when the first API call is made:

```csharp
Result<List<RelionLedgerJournalLine>> result =
    await relionService.GetNewJournalLinesAsync(DateTime.UtcNow.AddDays(-1), cancellationToken);

if (result.IsFailed)
{
    // IntegrationError with ErrorType.Failure
    // Check RelionSettings configuration
}
```

## RelionAuthMode Enum

| Value | Description |
|-------|-------------|
| `ApiKey` | Static API/subscription key via HTTP header |
| `OAuth` | OAuth 2.0 client credentials flow |

## See Also

- [[API-RelionService]] — service that uses these settings
- [[Configure-the-RELion-Connection]] — step-by-step setup guide
- [[Authentication-Modes]] — OAuth vs ApiKey comparison
