# Configure Resilience

> **Prerequisites:** a configured OData client registered via `AddIntegratoR(configuration)` (see [Configure OData](Configure-OData)).

The OData client is wrapped in a two-stage Polly pipeline — a retry policy and a circuit breaker. Both are configured through `ODataSettings.Resilience` and can be tuned independently per environment.

```json
{
  "ODataSettings": {
    "Resilience": {
      "EnableRetries": true,
      "RetryCount": 3,
      "UseCircuitBreaker": true,
      "CircuitBreakerThreshold": 5,
      "CircuitBreakerDurationInSeconds": 30
    }
  }
}
```

The defaults are tuned for production-grade D365 environments — retries with exponential backoff handle ordinary transient blips, the circuit breaker protects against cascading failures when D365 is genuinely down.

## What Gets Retried

The retry policy fires when **any** of the following conditions hold:

- HTTP status code is `408` (Request Timeout)
- HTTP status code is `429` (Too Many Requests)
- HTTP status code is `5xx` (any server error)
- The call throws `TaskCanceledException` (HTTP client timeout)

The policy is implemented as `Polly.Extensions.Http.HttpPolicyExtensions.HandleTransientHttpError().Or<TaskCanceledException>().OrResult(...)`. Non-transient errors (4xx other than 408/429) bypass the retry and surface immediately as `IntegrationError` in the `Result`.

## Backoff Formula

Each retry waits `2^attempt` seconds plus a random jitter of up to 25 % of the base delay:

| Attempt | Base delay | Max delay with jitter |
|---|---|---|
| 1 | 2.0 s | 2.5 s |
| 2 | 4.0 s | 5.0 s |
| 3 | 8.0 s | 10.0 s |
| 4 | 16.0 s | 20.0 s |
| 5 | 32.0 s | 40.0 s |

The jitter spreads out simultaneous retries from multiple workers so a recovering D365 environment is not hit by a thundering herd. The total worst-case wait for `RetryCount: 3` is roughly 17.5 seconds.

Each retry emits a `Warning` log entry to the `IntegratoR.OData.HttpRetry` logger with the attempt number, delay, and outcome reason:

```text
warn: IntegratoR.OData.HttpRetry[0] HTTP retry attempt 1 after 2000ms. Reason: ServiceUnavailable
```

Watch this logger to spot environments that retry frequently — chronic retries are a signal to investigate upstream rather than to increase `RetryCount`.

## What Counts as a Transient Failure for the Circuit Breaker

The circuit breaker uses the same `HandleTransientHttpError()` predicate. After `CircuitBreakerThreshold` consecutive transient failures, the breaker opens and fails all subsequent calls fast with `BrokenCircuitException`. After `CircuitBreakerDurationInSeconds`, the breaker transitions to half-open and lets a single probe request through:

- Probe succeeds → breaker closes, normal operation resumes
- Probe fails → breaker re-opens for another `CircuitBreakerDurationInSeconds`

Important: the consecutive-failures counter is **not** reset by a successful request mid-stream. The breaker counts every failure since the last `Closed` transition. This matches the typical Polly default semantics.

## Tuning Per Environment

The recommended tuning differs by environment:

| Environment | RetryCount | UseCircuitBreaker | Why |
|---|---|---|---|
| Local development | 1 | false | Fast feedback on errors, no breaker hold-down when restarting a dev sandbox |
| Test / staging | 3 | true | Production-like behaviour, useful for catching real transient issues |
| Production | 3 | true | Defaults — proven for D365 SaaS sandboxes and on-prem |

A programmatic override per environment:

```csharp
.ConfigureServices((context, services) =>
{
    services.AddIntegratoR(context.Configuration, integrator =>
    {
        integrator.ConfigureOData(settings =>
        {
            if (context.HostingEnvironment.IsDevelopment())
            {
                settings.Resilience.RetryCount = 1;
                settings.Resilience.UseCircuitBreaker = false;
            }
        });
    });
})
```

## Disabling Either Stage

The retry and circuit breaker are independent. Disabling one does not affect the other:

```jsonc
{
  "ODataSettings": {
    "Resilience": {
      "EnableRetries": false,         // no retries; failures surface immediately
      "UseCircuitBreaker": true       // breaker still trips on consecutive failures
    }
  }
}
```

Both can be disabled simultaneously for low-level debugging, but the production default is `true` for both.

## Authentication and Retries

The `ODataAuthenticationHandler` runs **inside** the retry policy chain. A `401 Unauthorized` response is treated as non-transient — the retry policy does not refire. Token refresh is handled separately by `OAuthAuthenticator`'s MSAL token cache, which proactively refreshes tokens before they expire.

If `OAuthAuthenticator` itself fails to acquire a token (wrong credentials, tenant misconfigured), the failure surfaces as `IntegrationError("OData.AuthenticationFailed", ..., Failure)` and short-circuits the call before any HTTP request is made — no retries, no circuit breaker counter increment.

## Cancellation Semantics

A `CancellationToken` passed to `mediator.Send(...)` is propagated through the full pipeline (MediatR behaviours → `IService<T>` → `ODataClientAdapter` → `HttpClient`). Cancellation during a retry wait abandons the wait immediately; cancellation during the HTTP call surfaces as `OperationCanceledException` (not caught by the framework).

The retry policy treats `TaskCanceledException` as a transient HTTP timeout — but only when it originates from `HttpClient.Timeout`. Cancellation via the caller's `CancellationToken` passes through unwrapped.

## Observability

- **`IntegratoR.OData.HttpRetry` logger** — each retry attempt logged at `Warning` with `RetryCount`, `DelayMs`, and `Reason`.
- **`IntegratoR.OData.Retry` logger** — the OData-level retry policy (for `ODataClientException` from PanoramicData) logs at `Warning` with the exception detail.
- **Circuit breaker transitions** — Polly emits `OnBreak`, `OnReset`, `OnHalfOpen` events. These are not wired to a logger by default; consumers needing this should subscribe to the policy via custom registration.

The smoke-test triggers shipped in `IntegratoR.SampleFunction` surface retry behaviour visibly — the log lines from a single smoke run reveal whether the environment is consistently retrying. See [Run Smoke Tests](Run-Smoke-Tests).

## See Also

- [Configure OData](Configure-OData) — the settings reference
- [Handle Errors](Handle-Errors) — what reaches the consumer when retries are exhausted
- [Authentication Modes](Authentication-Modes) — token caching and refresh
- [Troubleshoot Common Issues](Troubleshoot-Common-Issues) — diagnosing chronic retries
