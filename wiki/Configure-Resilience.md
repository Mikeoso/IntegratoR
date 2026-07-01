# Configure Resilience
> Last verified against v2.0.1

`AddIntegratoR` wires the OData client with a Polly retry policy and a circuit breaker. Tune both through the `Resilience` block of `ODataSettings` — no code change needed for the common case.

```json
{
  "ODataSettings": {
    "Url": "https://your-fo.operations.dynamics.com/data",
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

That block is the whole wiring step. Bind it in the host and register the framework:

```csharp
services.AddIntegratoR(configuration);
```

The defaults suit D365 SaaS sandboxes: retries with exponential backoff absorb ordinary transient blips, the breaker stops hammering an environment that is genuinely down.

## Options

| Property | Type | Default | Purpose |
|---|---|---|---|
| `EnableRetries` | `bool` | `true` | Toggles the retry policy. When `false`, a transient failure surfaces on the first attempt. |
| `RetryCount` | `int` | `3` | Retry attempts after the initial call. Recommended range 1–10. |
| `UseCircuitBreaker` | `bool` | `true` | Toggles the circuit breaker. Independent of `EnableRetries`. |
| `CircuitBreakerThreshold` | `int` | `5` | Consecutive transient failures before the breaker opens. |
| `CircuitBreakerDurationInSeconds` | `int` | `30` | Seconds the breaker stays open before it admits one probe request. |

> [!CAUTION]
> `RetryCount` 1–10 is a documented recommendation, not an enforced bound. A value outside that range is accepted as-is — a large count multiplies backoff waits and holds the request open for minutes.

## What gets retried

The retry policy fires on transient HTTP outcomes only:

- HTTP `408`, `429`, `500`, `502`, `503`, `504`
- `HttpRequestException` and `TaskCanceledException` (the `HttpClient` timeout)

Non-transient responses — a `4xx` other than `408`/`429`, including a `403` from a read-only field in a PATCH — bypass retries and surface immediately as a failed `Result<T>`. Retries target idempotent reads; a write that reached D365 is not replayed by design.

> [!CAUTION]
> Retry idempotent reads only. The retry predicate is applied to every request, so a `POST`/`PATCH`/`DELETE` that times out at the client (`TaskCanceledException`) is also retried. Treat writes as at-least-once and key on `IntegrationKey` for idempotency.

Each attempt waits `2^attempt` seconds plus jitter of up to 25% of that base delay. The jitter spreads simultaneous retries from multiple workers so a recovering environment is not hit by a thundering herd.

| Attempt | Base delay | Max delay with jitter |
|---|---|---|
| 1 | 2.0 s | 2.5 s |
| 2 | 4.0 s | 5.0 s |
| 3 | 8.0 s | 10.0 s |

Every attempt logs at `Warning` to the `IntegratoR.OData.HttpRetry` logger with the attempt number, delay, and reason:

```text
warn: IntegratoR.OData.HttpRetry[0] HTTP retry attempt 1 after 2000ms. Reason: ServiceUnavailable
```

Chronic retries are a signal to investigate the upstream environment, not to raise `RetryCount`.

## When the breaker is open

After `CircuitBreakerThreshold` consecutive transient failures the breaker opens and fails every call fast for `CircuitBreakerDurationInSeconds`, then admits one probe. A probe success closes it; a probe failure re-opens it for another window. While open, a call surfaces as a failed `Result<T>` with `ErrorType.Failure` — Polly's `BrokenCircuitException` is captured on the underlying `Exception`. Show the failure path with a real D365 write:

```csharp
LedgerJournalHeader header = new()
{
    DataAreaId = "USMF",
    JournalName = "GenJrn",
    Description = "Nightly import batch",
};

Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(header), cancellationToken);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    // Breaker open: error?.Type == ErrorType.Failure; the Polly
    // BrokenCircuitException is carried on error?.Exception.
    logger.LogWarning("Journal create failed: {Code}", error?.Code);
    return;
}

// A successful create echoes the entity with the server-assigned batch number.
string batchNumber = result.Value.JournalBatchNumber!;
```

> [!NOTE]
> The breaker counts *consecutive* failures: any success while it is closed resets the count to zero, so it opens only after `CircuitBreakerThreshold` failures with no intervening success. This matches Polly's classic breaker semantics.

The breaker predicate is `HandleTransientHttpError()` — it counts `HttpRequestException`, `5xx`, and `408`, but not `429`. A rate-limited environment is retried yet does not trip the breaker.

## Disable either stage

The two stages are independent — turn one off without touching the other:

```jsonc
{
  "ODataSettings": {
    "Resilience": {
      "EnableRetries": false,      // failures surface on the first attempt
      "UseCircuitBreaker": true    // breaker still trips on consecutive failures
    }
  }
}
```

For a development sandbox, override per environment via the configure delegate:

```csharp
services.AddIntegratoR(configuration, integrator =>
{
    integrator.ConfigureOData(settings =>
    {
        if (hostEnvironment.IsDevelopment())
        {
            settings.Resilience.RetryCount = 1;
            settings.Resilience.UseCircuitBreaker = false;
        }
    });
});
```

## Authentication and retries

The auth handler runs inside the policy chain, but a `401 Unauthorized` is non-transient, so it never retries. Token refresh is handled by `OAuthAuthenticator`'s MSAL cache, which refreshes proactively before expiry.

A token-acquisition failure short-circuits before any HTTP request — no retry, no breaker increment. It surfaces two ways depending on the code path:

- The failed `Result<T>` carries `IntegrationError` with `Code` `Auth.Msal.{code}` (for example `Auth.Msal.invalid_client`), `Type` `Failure`, and the MSAL exception on `Exception`.
- On the HTTP path the handler returns a `401` with `ReasonPhrase "Authentication failed"` — a generic phrase that never leaks tenant IDs or AADSTS codes.

> [!WARNING]
> The retry policy adds `.Or<TaskCanceledException>()` on top of `HandleTransientHttpError()`, and APIM surfaces some gateway timeouts as HTTP `400` rather than `408` or `504`. The observed effect is that a chronic `400` gets retried up to `RetryCount` times — the retry log shows `Reason: BadRequest` — before the request fails. See [Known Limitations](Known-Limitations).

## See Also

- [Configure OData](Configure-OData)
- [Handle Errors](Handle-Errors)
- [Authentication Modes](Authentication-Modes)
- [Troubleshoot Common Issues](Troubleshoot-Common-Issues)
