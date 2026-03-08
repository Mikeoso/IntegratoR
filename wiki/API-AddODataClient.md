# AddODataClient

Extension method that registers all OData infrastructure services, including the HTTP client with Polly resilience policies, authentication handler, and generic service implementations.

## Use the Extension Method

```csharp
// From IConfiguration (reads "ODataSettings" section)
services.AddODataClient(configuration);

// Programmatic configuration
services.AddODataClient(options =>
{
    options.Url = "https://your-environment.operations.dynamics.com/data";
    options.AuthMode = ODataAuthMode.OAuth;
    options.ClientId = "your-client-id";
    options.ClientSecret = "your-secret";
    options.TenantId = "your-tenant-id";
    options.Resource = "https://your-environment.operations.dynamics.com";
});
```

## Overloads

### IConfiguration Overload

```csharp
public static IServiceCollection AddODataClient(
    this IServiceCollection services,
    IConfiguration configuration)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `services` | `IServiceCollection` | The DI service collection |
| `configuration` | `IConfiguration` | Configuration containing an `"ODataSettings"` section |

### Action Overload

```csharp
public static IServiceCollection AddODataClient(
    this IServiceCollection services,
    Action<ODataSettings> configureOptions)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `services` | `IServiceCollection` | The DI service collection |
| `configureOptions` | `Action<ODataSettings>` | Delegate to configure settings programmatically |

## What Gets Registered

| Registration | Lifetime | Purpose |
|-------------|----------|---------|
| `ODataSettings` | Options | Bound from config or delegate |
| `ODataAuthenticationHandler` | Transient | Adds auth headers (Bearer token or API key) to requests |
| `ODataMetadataProvider` | Singleton | Resolves OData metadata (local file or remote) |
| `HttpClient "ODataClient"` | Named | HTTP client with Polly policies and auth handler |
| `ODataClient` (PanoramicData) | Singleton | Underlying OData client library |
| `IODataClientAdapter` | Singleton | Adapter wrapping PanoramicData's ODataClient |
| `AsyncRetryPolicy` | Singleton | Polly retry policy for OData operation-level retries |
| `IService<>` | Scoped | Resolves to `ODataService<>` |
| `IODataService<>` | Scoped | Resolves to `ODataService<>` |
| `IODataBatchService<>` | Scoped | Resolves to `ODataService<>` |

## Polly Resilience Policies

### HTTP Retry Policy

Applied at the `HttpClient` level. Retries on transient HTTP errors with exponential backoff and jitter.

**Triggers:**
- Transient HTTP errors (5xx, 408)
- `TaskCanceledException` (timeouts)
- HTTP 429 (Too Many Requests)

**Backoff formula:**
```
delay = 2^attempt + random(0, 2^attempt * 0.25) ms
```

| Attempt | Base Delay | With Jitter (approx) |
|---------|-----------|---------------------|
| 1 | 2s | 2.0 - 2.5s |
| 2 | 4s | 4.0 - 5.0s |
| 3 | 8s | 8.0 - 10.0s |

```csharp
// Configured via ODataSettings
{
    "EnableRetries": true,
    "RetryCount": 3
}
```

When `EnableRetries` is `false`, a no-op policy is used.

### Circuit Breaker Policy

Prevents cascading failures by stopping requests after consecutive failures.

| State | Behaviour |
|-------|-----------|
| **Closed** | Requests flow normally |
| **Open** | All requests are blocked for `CircuitBreakerDurationSeconds` |
| **Half-Open** | One test request is allowed through to check recovery |

```csharp
// Configured via ODataSettings
{
    "UseCircuitBreaker": true,
    "CircuitBreakerThreshold": 5,
    "CircuitBreakerDurationSeconds": 30
}
```

### OData Operation Retry Policy

A separate `AsyncRetryPolicy` is registered for retrying at the OData operation level (inside `ODataService`). This handles `ODataClientException` with transient status codes:

- 408 (Request Timeout)
- 429 (Too Many Requests)
- 5xx (Server errors)

## See Examples

### Typical Azure Functions Setup

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationServices();
        services.AddODataClient(context.Configuration);
    })
    .Build();

host.Run();
```

### With Programmatic Configuration and Key Vault

```csharp
services.AddODataClient(options =>
{
    options.Url = configuration["D365:Url"]!;
    options.AuthMode = ODataAuthMode.OAuth;
    options.ClientId = configuration["D365:ClientId"]!;
    options.ClientSecret = keyVaultClient.GetSecret("d365-client-secret").Value;
    options.TenantId = configuration["D365:TenantId"]!;
    options.Resource = configuration["D365:Resource"]!;
    options.MetadataFilePath = "metadata.xml";
});
```

### Error Handling

Registration errors do not surface at startup. Failures occur when the first OData request is made:

```csharp
// Missing or invalid settings -> first service call fails
Result<LedgerJournalHeader> result = await service.GetByKeyAsync(["USMF", "JRN-001"], cancellationToken);

if (result.IsFailed)
{
    // ErrorType.Failure with HTTP error details
    // Check ODataSettings configuration
}
```

Circuit breaker open state is also returned as a failed `Result`:

```csharp
// After 5 consecutive failures
Result<LedgerJournalHeader> result = await service.GetByKeyAsync(["USMF", "JRN-001"], cancellationToken);
// result.IsFailed == true
// BrokenCircuitException wrapped in IntegrationError
```

## See Also

- [[API-ODataSettings]] — all available configuration options
- [[API-ODataService]] — the service implementations registered by this method
- [[Register-Services-in-Your-Host]] — full host setup guide
