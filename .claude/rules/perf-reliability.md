# Performance and Reliability

> Async *language* style (`ConfigureAwait(false)`, no sync-over-async, no `Task.Run`) is a coding standard — see the `csharp-coding-standards` skill and the CLAUDE.md Hard Rules. This file owns runtime reliability: HTTP, resilience, caching, logging.

## Outbound HTTP

- Every HTTP call must have a timeout (configured via `ODataSettings.Timeout`).
- `CancellationToken` wired through to all `HttpClient` calls.
- Retry ONLY for transient, idempotent operations — Polly handles this via `ODataResilienceSettings`.
- Never retry infinite — `RetryCount` has a valid range of 1-10.
- Circuit breaker enabled by default — prevents cascading failures when D365 is down.

## Resilience Configuration

- Resilience settings live under `ODataSettings.Resilience` — respect them, don't hardcode.
- `EnableRetries` and `UseCircuitBreaker` are independently toggleable.
- Exponential backoff with jitter to prevent thundering herd.
- Retried status codes: 408, 429, 500, 502, 503, 504.

## Caching

- Cache service calls should fail fast — don't block the request pipeline.
- `ICacheableQuery<T>` interface marks queries that can be cached.
- Pipeline order matters: Logging -> Validation -> Caching -> Handler.

## Logging

- Avoid high-cardinality fields in structured logs (user IDs as dimensions are fine, request bodies are not).
- Never log credentials, tokens, or subscription keys.
- Avoid expensive structured logs in hot paths (per-request handlers).
