# Configure Retry and Circuit Breaker

IntegratoR uses Polly resilience policies to handle transient failures when communicating with D365 F&O. Retries and circuit breaker are enabled by default and configured via `ODataSettings`.

> **Prerequisites:** [[Install-the-Framework]]

## Configure via appsettings.json

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

| Setting | Default | Description |
|---------|---------|-------------|
| `EnableRetries` | `true` | Enable automatic retry on transient failures |
| `RetryCount` | `3` | Number of retry attempts (valid range: 1-10) |
| `UseCircuitBreaker` | `true` | Enable the circuit breaker pattern |
| `CircuitBreakerThreshold` | `5` | Consecutive failures before the circuit opens |
| `CircuitBreakerDurationSeconds` | `30` | Seconds the circuit stays open before testing recovery |

## How Retries Work

When a transient failure occurs, Polly retries the request with exponential backoff plus jitter:

```
Attempt 1: immediate
Attempt 2: ~2 seconds + jitter
Attempt 3: ~4 seconds + jitter
Attempt 4: ~8 seconds + jitter  (if RetryCount = 4)
```

Jitter adds a random delay to prevent multiple clients from retrying simultaneously (the "thundering herd" problem).

**Retried HTTP status codes:**

| Status Code | Meaning |
|-------------|---------|
| 408 | Request Timeout |
| 429 | Too Many Requests (rate limited) |
| 500 | Internal Server Error |
| 502 | Bad Gateway |
| 503 | Service Unavailable |
| 504 | Gateway Timeout |

## Two Retry Layers

IntegratoR applies retries at two levels:

1. **HttpClient level** -- Polly policies on the `HttpClient` handle transient HTTP failures (timeouts, 5xx responses). This covers all HTTP communication.
2. **OData operation level** -- retries on the OData client operations catch failures specific to OData request processing.

Both layers work together. An OData operation retry may trigger multiple HTTP-level retries if the underlying HTTP call also fails transiently.

## How the Circuit Breaker Works

The circuit breaker prevents cascading failures by stopping requests when D365 F&O is consistently failing:

```
CLOSED (normal)
    |
    v
[5 consecutive failures]  (CircuitBreakerThreshold)
    |
    v
OPEN (all requests immediately fail)
    |
    v
[30 seconds pass]  (CircuitBreakerDurationSeconds)
    |
    v
HALF-OPEN (one test request allowed)
    |
    v
Success? -> CLOSED (resume normal operation)
Failure? -> OPEN (restart the timer)
```

While the circuit is open, all requests fail immediately with a `BrokenCircuitException` wrapped in a `Result.Fail`. This avoids wasting resources on calls that are likely to fail.

## Disable Resilience for Development

During local development or testing, you may want to disable retries and circuit breaker to fail fast:

```json
{
  "ODataSettings": {
    "EnableRetries": false,
    "UseCircuitBreaker": false
  }
}
```

## When Things Go Wrong

**All retries exhausted** -- after `RetryCount` attempts, the final failure is returned as the result:

```
result.IsFailed  = true
result.GetError().Code     = "OData.Error"
result.GetError().Message  = "Service unavailable after 3 retry attempts."
result.GetError().Type     = ErrorType.Failure
```

**Circuit breaker open** -- requests fail immediately without reaching D365:

```
result.IsFailed  = true
result.GetError().Code     = "OData.Error"
result.GetError().Message  = "The circuit breaker is open. Requests are blocked."
result.GetError().Type     = ErrorType.Failure
```

**429 rate limiting** -- D365 F&O throttles requests when API limits are exceeded. The retry policy handles this automatically, but if you consistently hit rate limits, consider reducing request frequency or using batch operations.

## See Also

- [[Handle-Errors-with-Result]] — inspect errors returned after retries are exhausted
- [[Batch-Multiple-Operations]] — bulk operations that benefit from resilience policies
- [[Cache-Query-Results]] — reduce external calls alongside retry policies
