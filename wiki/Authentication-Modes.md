# Authentication Modes

IntegratoR supports two authentication modes for connecting to external services: **OAuth 2.0 client credentials** and **API Key** (subscription key). Both the OData client (D365 F&O) and the RELion client support both modes.

> **Prerequisites:** [[API-ODataSettings]], [[API-RelionSettings]]

## Compare the Modes

| Aspect | OAuth | ApiKey |
|--------|-------|--------|
| **Protocol** | OAuth 2.0 client credentials flow | Static subscription key in HTTP header |
| **Token management** | Automatic (acquire, cache, refresh) | None (static key) |
| **Use when** | Direct access to D365 F&O or RELion | Access via Azure API Management gateway |
| **Security** | Azure AD service principal with scoped permissions | Shared secret managed by the API gateway |
| **Recommended for** | Production direct connections | Gateway-mediated access |
| **Required settings** | ClientId, ClientSecret, TenantId, Resource | SubscriptionKey, SubscriptionHeaderKey |
| **Authentication handler** | Acquires Bearer token, adds `Authorization` header | Adds subscription key to configured header |

## OAuth 2.0 (Client Credentials)

The recommended mode for direct service-to-service communication with D365 F&O. Uses Azure AD to acquire a Bearer token.

### How It Works

```
IntegratoR                    Azure AD                    D365 F&O
    |                            |                           |
    |-- POST /oauth2/token ----->|                           |
    |   (client_id, secret,      |                           |
    |    resource, grant_type)   |                           |
    |                            |                           |
    |<--- Bearer token ----------|                           |
    |                            |                           |
    |-- GET /data/Entity --------|-------------------------->|
    |   Authorization: Bearer ...|                           |
    |                            |                           |
    |<--- 200 OK + data ---------|---------------------------|
```

### OData Configuration

```json
{
  "ODataSettings": {
    "Url": "https://your-env.operations.dynamics.com/data",
    "AuthMode": "OAuth",
    "ClientId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
    "ClientSecret": "your-secret",
    "TenantId": "ffffffff-gggg-hhhh-iiii-jjjjjjjjjjjj",
    "Resource": "https://your-env.operations.dynamics.com"
  }
}
```

### RELion Configuration

```json
{
  "RelionSettings": {
    "Url": "https://relion-api.example.com",
    "Company": "USMF",
    "AuthMode": "OAuth",
    "ClientId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
    "ClientSecret": "your-secret",
    "TenantId": "ffffffff-gggg-hhhh-iiii-jjjjjjjjjjjj",
    "Resource": "https://relion-api.example.com"
  }
}
```

### Prerequisites

1. Azure AD App Registration with a client secret
2. API permissions granted for the target service (D365 F&O or RELion)
3. The service principal added as a user in D365 F&O with appropriate security roles

## API Key (Subscription Key)

Used when D365 F&O or RELion is fronted by an API gateway such as Azure API Management (APIM). The gateway handles authentication to the backend; the client only needs to present a subscription key.

### How It Works

```
IntegratoR                Azure APIM                  D365 F&O
    |                         |                          |
    |-- GET /d365/Entity ---->|                          |
    |   Ocp-Apim-Sub-Key: ... |                          |
    |                         |-- GET /data/Entity ----->|
    |                         |   Authorization: Bearer  |
    |                         |   (APIM handles auth)    |
    |                         |                          |
    |                         |<--- 200 OK + data -------|
    |<--- 200 OK + data ------|                          |
```

### OData Configuration

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

### RELion Configuration

```json
{
  "RelionSettings": {
    "Url": "https://your-apim.azure-api.net/relion",
    "Company": "USMF",
    "AuthMode": "ApiKey",
    "SubscriptionKey": "your-subscription-key",
    "SubscriptionHeaderKey": "Ocp-Apim-Subscription-Key"
  }
}
```

### When to Use

- D365 F&O is exposed through Azure API Management
- You want centralised rate limiting, monitoring, and access control
- Multiple backend services are consolidated behind a single gateway
- The gateway handles OAuth token acquisition on behalf of the client

## Choosing a Mode

```
Do you access D365 F&O / RELion directly?
    |
    +-- Yes --> Use OAuth
    |
    +-- No, via API Management gateway --> Use ApiKey
```

| Scenario | Recommended Mode |
|----------|-----------------|
| Azure Function calling D365 F&O directly | OAuth |
| Azure Function calling D365 F&O through APIM | ApiKey |
| Local development with direct D365 access | OAuth |
| Local development with APIM sandbox | ApiKey |
| Multi-tenant SaaS with per-customer gateways | ApiKey |

## Handle Errors

### OAuth Failures

```csharp
// Invalid credentials or expired secret
Result<LedgerJournalHeader> result = await service.GetByKeyAsync(["USMF", "JRN-001"], cancellationToken);
// Result: Result<LedgerJournalHeader> — Failure when credentials are invalid

if (result.IsFailed)
{
    // IntegrationError with ErrorType.Failure
    // Inner exception: HttpRequestException (401 Unauthorized)
    // Check ClientId, ClientSecret, TenantId, Resource
}
```

### ApiKey Failures

```csharp
// Invalid or expired subscription key
Result<LedgerJournalHeader> result = await service.GetByKeyAsync(["USMF", "JRN-001"], cancellationToken);
// Result: Result<LedgerJournalHeader> — Failure when subscription key is rejected

if (result.IsFailed)
{
    // IntegrationError with ErrorType.Failure
    // HTTP 401 or 403 from the API gateway
    // Check SubscriptionKey and SubscriptionHeaderKey
}
```

## Auth Mode Enums

### ODataAuthMode

```csharp
public enum ODataAuthMode
{
    ApiKey,  // Static subscription key via HTTP header
    OAuth    // OAuth 2.0 client credentials flow (recommended)
}
```

### RelionAuthMode

```csharp
public enum RelionAuthMode
{
    ApiKey,  // Static subscription key via HTTP header
    OAuth    // OAuth 2.0 client credentials flow
}
```

## See Also

- [[API-ODataSettings]] — full OData settings reference
- [[API-RelionSettings]] — full RELion settings reference
- [[Configure-the-OData-Connection]] — step-by-step OData setup
- [[Configure-the-RELion-Connection]] — step-by-step RELion setup
