# Resilience

```json
{
  "ODataSettings": {
    "Url": "https://your-environment.operations.dynamics.com/data",
    "EnableRetries": true,
    "RetryCount": 3,
    "UseCircuitBreaker": true,
    "CircuitBreakerThreshold": 5,
    "CircuitBreakerDurationSeconds": 30
  }
}
```

Retries and circuit breaker are enabled by default via [[Configuration]] and powered by Polly.

## Retry Policy

Transient failures are retried with exponential backoff plus jitter (to avoid thundering herd):

```
Attempt 1: immediate
Attempt 2: ~2s + jitter
Attempt 3: ~4s + jitter
Attempt 4: ~8s + jitter  (if RetryCount = 4)
```

Retried status codes: `408`, `429`, `500`, `502`, `503`, `504`.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `EnableRetries` | `bool` | `true` | Enable automatic retry on transient failures |
| `RetryCount` | `int` | `3` | Number of retry attempts (valid range: 1–10) |

IntegratoR applies retries at two levels:

1. **HttpClient level** — Polly policies on the `HttpClient` handle transient HTTP failures (timeouts, 5xx).
2. **OData operation level** — retries on OData client operations catch failures specific to request processing.

Both layers work together. An OData operation retry may trigger multiple HTTP-level retries if the underlying call also fails transiently.

## Circuit Breaker

The circuit breaker stops requests when D365 F&O is consistently failing:

```
CLOSED (normal)  ->  [5 consecutive failures]  ->  OPEN (requests fail immediately)
    ->  [30s pass]  ->  HALF-OPEN (one test request)
    ->  Success? CLOSED  /  Failure? OPEN again
```

While open, all requests fail immediately with a `BrokenCircuitException` wrapped in `Result.Fail()`.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `UseCircuitBreaker` | `bool` | `true` | Enable circuit breaker pattern |
| `CircuitBreakerThreshold` | `int` | `5` | Consecutive failures before the circuit opens |
| `CircuitBreakerDurationSeconds` | `int` | `30` | Seconds the circuit stays open before recovery |

## Disabling for Development

```json
{
  "ODataSettings": {
    "EnableRetries": false,
    "UseCircuitBreaker": false
  }
}
```

Or programmatically:

```csharp
services.AddODataClient(options =>
{
    options.EnableRetries = false;       // fail fast during development
    options.UseCircuitBreaker = false;
});
```
