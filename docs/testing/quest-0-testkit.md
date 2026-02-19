# Quest 0: Foundation -- IntegratoR.TestKit

**Project**: `tests/IntegratoR.TestKit/`
**Purpose**: Shared test infrastructure referenced by all test projects.
**Dependencies**: `IntegratoR.Abstractions`, `FluentAssertions`

---

## Mission 0.1: Test Entity Doubles

| Class | Purpose |
|---|---|
| `TestEntity : BaseEntity<string>` | Composite key entity (Id + PartitionKey) for generic handler tests |
| `TestSingleKeyEntity : BaseEntity<int>` | Single key entity for simple key scenarios |
| `TestEntityWithODataAttributes : BaseEntity<string>` | Entity with `[ODataField(IgnoreOnCreate)]`, `[ODataField(IgnoreOnUpdate)]` for payload tests |

---

## Mission 0.2: Test Builders

| Class | Purpose |
|---|---|
| `TestEntityBuilder` | Fluent builder for `TestEntity` with sensible defaults |

---

## Mission 0.3: Custom FluentAssertions for Result Types

| Class | Key Methods |
|---|---|
| `ResultAssertions` | `.BeSuccessful()`, `.BeFailed()`, `.HaveErrorCode(string)`, `.HaveErrorType(ErrorType)` |
| `ResultAssertions<T>` | Same + `.HaveValue(T)` |

---

## Mission 0.4: Fake Infrastructure

| Class | Purpose |
|---|---|
| `FakeHttpMessageHandler` | Queued HTTP responses for DelegatingHandler tests, tracks sent requests |
| `FakeCacheService : ICacheService` | In-memory dictionary-based cache with test helpers (`Contains`, `Count`, `Clear`) |
| `TestCacheableQuery<T> : ICacheableQuery<T>` | Configurable cacheable query for CachingBehaviour tests |
