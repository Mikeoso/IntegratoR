# Configure Resilience
> Last verified against v3.0.0

`AddIntegratoR` wires the OData client with Polly retry and a circuit breaker. Tune both through the `Resilience` block of `ODataSettings` — no code change needed for the common case.

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
| `EnableRetries` | `bool` | `true` | Toggles both IntegratoR retry policies (HTTP and operation). PanoramicData's internal loop is unaffected — see below. |
| `RetryCount` | `int` | `3` | Retry attempts after the initial call, per IntegratoR retry policy. Recommended range 1–10. |
| `UseCircuitBreaker` | `bool` | `true` | Toggles the circuit breaker. Independent of `EnableRetries`. |
| `CircuitBreakerThreshold` | `int` | `5` | Consecutive transient failures before the breaker opens. |
| `CircuitBreakerDurationInSeconds` | `int` | `30` | Seconds the breaker stays open before it admits one probe request. |

> [!CAUTION]
> `RetryCount` 1–10 is a documented recommendation, not an enforced bound. A value outside that range is accepted as-is — a large count multiplies backoff waits and holds the request open for minutes.

## What gets retried

Three retry layers sit between your command or query and D365, and none of them checks the HTTP method:

| Layer | Wraps | Retries on | Attempts and delay |
|---|---|---|---|
| HTTP policy on the named `ODataClient` `HttpClient` | Every request: reads, single writes, composite-key writes, each `$batch` chunk POST, each `ContinueOnError` per-item write | `408`, `429`, any `5xx`, `HttpRequestException` | `RetryCount`, exponential backoff below |
| Operation policy in `ODataService<T>` | Each single-entity call — create, update, delete, get, find, query, count — but not batch calls | An OData error with status `408`, `429`, or any `5xx`; `TaskCanceledException` | `RetryCount`, exponential backoff below |
| PanoramicData.OData.Client's internal loop | Calls routed through PanoramicData — creates, reads, counts, single-key updates and deletes — but not composite-key writes or `$batch` | Any `5xx`, `HttpRequestException`, the `HttpClient` timeout | Library default: 3 retries, fixed 1 s. IntegratoR leaves it unset, and `EnableRetries` does not switch it off |

The HTTP policy's predicate also names `TaskCanceledException`, but an `HttpClient` timeout cancels the token that policy runs under, so it logs one retry attempt and then gives up; the operation policy and PanoramicData do retry a timeout. Non-transient responses — a `4xx` other than `408`/`429`, including a `403` from a read-only field in a PATCH — are retried by no layer and surface immediately as a failed `Result<T>`.

> [!WARNING]
> Writes are retried. A `POST`, `PATCH`, or `DELETE` — every `$batch` chunk POST included — that fails with `408`, `429`, or `5xx` is sent again, and a single write that times out is re-sent by the operation policy. D365 may already have committed the first attempt, so a retried create can insert a duplicate record. Treat writes as at-least-once: before you re-drive a failed create, look the record up by a business key such as `LedgerJournalHeader.IntegrationKey`. A fix is planned after v3.0.0 — see [Known Limitations](Known-Limitations).

The layers nest, so each one multiplies the attempts of the layer inside it — a slow gateway such as APIM stretches the wait before a timeout surfaces. With the circuit breaker on (the default), the call fails fast once the breaker opens instead of exhausting the remaining attempts.

Each attempt waits `2^attempt` seconds plus jitter of up to 25% of that base delay. The jitter spreads simultaneous retries from multiple workers so a recovering environment is not hit by a thundering herd.

| Attempt | Base delay | Max delay with jitter |
|---|---|---|
| 1 | 2.0 s | 2.5 s |
| 2 | 4.0 s | 5.0 s |
| 3 | 8.0 s | 10.0 s |

The two IntegratoR policies share this backoff. The HTTP policy logs every attempt at `Warning` to the `IntegratoR.OData.HttpRetry` logger with the attempt number, delay, and reason; the operation policy logs to `IntegratoR.OData.Retry`. PanoramicData's internal retries are not logged — IntegratoR passes it no logger.

```text
warn: IntegratoR.OData.HttpRetry[0] HTTP retry attempt 1 after 2187ms. Reason: ServiceUnavailable
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
    // Breaker open: error?.Code == "LedgerJournalHeader.UnexpectedError", error?.Type == ErrorType.Failure;
    // the Polly BrokenCircuitException is carried on error?.Exception.
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
      "EnableRetries": false,      // both IntegratoR retry policies off
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

`ODataAuthenticationHandler` is registered before the retry and breaker handlers, so it is the outermost handler on the `HttpClient`: it attaches the token, and the HTTP retry and breaker run inside it. A `401 Unauthorized` from D365 is non-transient, so no layer retries it. `OAuthAuthenticator` caches the token and re-acquires it five minutes before expiry.

A token-acquisition failure short-circuits in the auth handler, before Polly sees the request — no retry, no breaker increment, no request to D365. It surfaces two ways:

- The raw HTTP response is a `401` with `ReasonPhrase "Authentication failed"` — a generic phrase that never leaks tenant IDs or AADSTS codes.
- Through `IMediator`, the failed `Result<T>` carries `Code` `{Entity}.Unauthorized` (for example `LedgerJournalHeader.Unauthorized`) and `Type` `Failure`. The handler discards the MSAL error and nothing logs it; only a direct `IAuthenticator.GetAccessTokenAsync` call returns `Auth.Msal.{code}` — see [Authentication Modes](Authentication-Modes).

## See Also

- [Configure OData](Configure-OData)
- [Handle Errors](Handle-Errors)
- [Authentication Modes](Authentication-Modes)
- [Troubleshoot Common Issues](Troubleshoot-Common-Issues)
