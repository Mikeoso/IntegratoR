# Performance

## General Principles

- Profile before optimizing — do not guess at bottlenecks
- Measure with benchmarks when making performance claims
- Prefer algorithmic improvements over micro-optimizations

## Async & Concurrency

- Propagate `CancellationToken` through every async call chain
- Never block on async code (no `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` in application code)
- Use `ConfigureAwait(false)` in library code (non-UI contexts)

## Caching

- Only cache successful results — never cache errors or partial failures
- Always set explicit expiration (absolute or sliding)
- Use cache keys that include all varying parameters

## Resilience

- Retry only transient, idempotent failures
- Use exponential backoff with jitter to avoid thundering herd
- Pair retries with a circuit breaker to prevent cascading failures
- Log every retry attempt with the retry count and delay
