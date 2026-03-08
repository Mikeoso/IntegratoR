# Configure the RELion Connection

Set up the RELion API client with authentication and register it in your dependency injection container.

> **Prerequisites:** [[Install-the-Framework]], [[Register-Services-in-Your-Host]]

## Add Settings to appsettings.json

RELion supports two authentication modes: `ApiKey` and `OAuth`.

**API Key authentication:**

```json
{
  "RelionSettings": {
    "Url": "https://api.relion.example.com",
    "Timeout": 120,
    "Company": "My Company",
    "AuthMode": "ApiKey",
    "SubscriptionKey": "your-subscription-key",
    "SubscriptionHeaderKey": "Ocp-Apim-Subscription-Key"
  }
}
```

**OAuth authentication:**

```json
{
  "RelionSettings": {
    "Url": "https://api.relion.example.com",
    "Timeout": 120,
    "Company": "My Company",
    "AuthMode": "OAuth",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "TenantId": "your-tenant-id",
    "Resource": "https://api.relion.example.com"
  }
}
```

| Setting | Required | Default | Description |
|---|---|---|---|
| `Url` | Yes | -- | Base URL of the RELion API |
| `Timeout` | No | `120` | HTTP request timeout in seconds |
| `Company` | No | `""` | Company name to target for API requests |
| `AuthMode` | Yes | -- | `ApiKey` or `OAuth` |
| `SubscriptionKey` | ApiKey mode | `""` | API Management subscription key |
| `SubscriptionHeaderKey` | ApiKey mode | `""` | Header name for the subscription key |
| `ClientId` | OAuth mode | `""` | OAuth client ID |
| `ClientSecret` | OAuth mode | `""` | OAuth client secret |
| `TenantId` | OAuth mode | `""` | Azure AD tenant ID |
| `Resource` | OAuth mode | `""` | OAuth resource / audience URI |

## Register the RELion Client

In your `Program.cs` or host setup, call `AddRelionClient`:

```csharp
using IntegratoR.RELion.Common.Extensions;

var host = new HostBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddRelionClient(context.Configuration);
    })
    .Build();
```

This single call registers:
- `RelionSettings` from the `"RelionSettings"` configuration section
- `RelionAuthenticationHandler` for automatic authentication on outbound requests
- An `HttpClient` named `"RelionApiClient"` with the auth handler attached
- `IRelionService` as `RelionService` (scoped lifetime)
- All MediatR handlers from the RELion assembly

## Verify the Configuration

Inject `IRelionService` and make a test call:

```csharp
using IntegratoR.RELion.Interfaces.Services;

public class HealthCheckFunction
{
    private readonly IRelionService _relionService;

    public HealthCheckFunction(IRelionService relionService)
    {
        _relionService = relionService;
    }

    public async Task<bool> CheckConnectivity(CancellationToken cancellationToken)
    {
        var result = await _relionService.GetCompanyByNameAsync("My Company", cancellationToken);
        return result.IsSuccess;
    }
}
```

## When Things Go Wrong

If the `Url` is missing or incorrect, you get an HTTP-level error when making your first API call:

```csharp
var result = await relionService.GetCompanyByNameAsync("My Company", cancellationToken);

// result.IsFailed == true
// Error code: "Relion.ApiError"
// Error message: "API returned status code InternalServerError."
```

If OAuth credentials are wrong, the `RelionAuthenticationHandler` fails to obtain a token and the request fails before reaching the RELion API.

If the company name does not match any company in RELion:

```csharp
var result = await relionService.GetCompanyByNameAsync("NonExistent", cancellationToken);

// result.IsFailed == true
// Error code: "Relion.CompanyNotFound"
// Error message: "Company with name 'NonExistent' not found."
```

## See Also

- [[Query-RELion-Data]] — use the configured client to fetch data
- [[Register-Services-in-Your-Host]] — full DI registration order
- [[Set-Up-an-Azure-Functions-Host]] — complete host setup with all services
