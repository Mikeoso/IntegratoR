# Context7 Query Patterns by Ecosystem

Detailed query patterns organised by library ecosystem. Use these as templates for effective Context7 lookups.

## IntegratoR Project Libraries

These libraries are used in the IntegratoR codebase. Resolve IDs and query patterns are pre-mapped for convenience.

### MediatR (CQRS)

```
libraryName: "MediatR"
```

| Task | Query |
|------|-------|
| Pipeline behaviours | `"IPipelineBehavior request response pipeline"` |
| Notification handlers | `"INotificationHandler publish notification"` |
| Request pre/post processors | `"IRequestPreProcessor IRequestPostProcessor"` |
| DI registration | `"AddMediatR ServiceCollection registration"` |
| Streaming requests | `"IStreamRequest stream handler"` |

### FluentResults

```
libraryName: "FluentResults"
```

| Task | Query |
|------|-------|
| Creating results | `"Result.Ok Result.Fail creating results"` |
| Error handling | `"IError custom error reason metadata"` |
| Result pattern matching | `"Result.IsSuccess IsFailed pattern matching"` |
| Merging results | `"Merge results combine multiple Result"` |
| Custom errors | `"custom error class extending IError"` |

### FluentValidation

```
libraryName: "FluentValidation"
```

| Task | Query |
|------|-------|
| Validator creation | `"AbstractValidator RuleFor validation rules"` |
| Async validation | `"MustAsync custom async validation"` |
| Conditional rules | `"When Unless conditional validation"` |
| Custom validators | `"custom property validator IRuleBuilder"` |
| DI integration | `"AddValidatorsFromAssembly dependency injection"` |

### Polly (Resilience)

```
libraryName: "Polly"
```

| Task | Query |
|------|-------|
| Retry policy | `"retry policy exponential backoff jitter"` |
| Circuit breaker | `"circuit breaker advanced options duration"` |
| Resilience pipeline | `"ResiliencePipelineBuilder AddRetry AddCircuitBreaker"` |
| Timeout | `"timeout strategy optimistic pessimistic"` |
| DI with HttpClient | `"AddResilienceHandler HttpClient integration"` |

### PanoramicData.OData.Client

```
libraryName: "PanoramicData.OData.Client"
```

| Task | Query |
|------|-------|
| CRUD operations | `"CreateAsync GetByKeyAsync UpdateAsync DeleteAsync CRUD"` |
| Batch operations | `"CreateBatch CreateChangeset batch atomic operations"` |
| Filtering (LINQ) | `"Filter LINQ expression strongly typed filtering"` |
| Filtering (string) | `"Filter raw OData filter string query"` |
| Query builder | `"For Select Expand OrderBy Skip Top Count query"` |
| Pagination | `"GetAllAsync automatic pagination next link"` |
| Exception handling | `"ODataNotFoundException ODataClientException typed exceptions"` |
| Configuration | `"ODataClientOptions BaseUrl ConfigureRequest JsonSerializerOptions"` |
| Authentication | `"ConfigureRequest Authorization Bearer token headers"` |

### Simple.OData.Client (REPLACED — use PanoramicData.OData.Client)

```
libraryName: "Simple.OData.Client"
```

> **Note:** Simple.OData.Client 6.0.1 is unmaintained (last commit May 2024). Replaced by PanoramicData.OData.Client in the IntegratoR codebase.

| Task | Query |
|------|-------|
| CRUD operations | `"FindEntriesAsync InsertEntryAsync UpdateEntryAsync"` |
| Batch operations | `"batch request ODataBatch"` |
| Filtering | `"Filter Where clause OData query"` |
| Navigation properties | `"expand navigation property linked entities"` |
| Authentication | `"HttpClient authentication bearer token"` |

### StackExchange.Redis

```
libraryName: "StackExchange.Redis"
```

| Task | Query |
|------|-------|
| Connection setup | `"ConnectionMultiplexer Connect configuration"` |
| String operations | `"StringSet StringGet cache operations"` |
| Expiration | `"SetExpiry TimeSpan absolute sliding"` |
| Pub/Sub | `"Subscribe Publish ISubscriber channel"` |
| Distributed lock | `"LockTake LockRelease distributed locking"` |

### Newtonsoft.Json

```
libraryName: "Newtonsoft.Json"
```

| Task | Query |
|------|-------|
| Custom converters | `"JsonConverter ReadJson WriteJson custom"` |
| Serialization settings | `"JsonSerializerSettings DefaultSettings"` |
| Attributes | `"JsonProperty JsonIgnore JsonConverter attribute"` |
| Polymorphic serialization | `"TypeNameHandling JsonSubtypes derived types"` |
| Contract resolver | `"IContractResolver custom property resolution"` |

## NuGet Ecosystem (.NET)

### General Query Patterns

```
# Setup and registration
resolve-library-id(libraryName: "[Package]", query: "[package] dependency injection setup")
query-docs(libraryId: "/resolved-id", query: "AddServices ServiceCollection registration getting started")

# Configuration via options pattern
query-docs(libraryId: "/resolved-id", query: "Configure options IOptions appsettings.json")

# Middleware/pipeline
query-docs(libraryId: "/resolved-id", query: "middleware pipeline Use Map Run")
```

### Common NuGet Libraries

| Library | libraryName | Typical Query Focus |
|---------|-------------|---------------------|
| AutoMapper | `"AutoMapper"` | Profile creation, DI, projection |
| Serilog | `"Serilog"` | Sink configuration, enrichers, structured logging |
| Dapper | `"Dapper"` | Query, QueryAsync, multi-mapping |
| Moq | `"Moq"` | Setup, Returns, Verify, Callback |
| xUnit | `"xUnit"` | Theory, InlineData, fixtures, assertions |
| NSubstitute | `"NSubstitute"` | Returns, Received, Arg matching |
| Bogus | `"Bogus"` | Faker, RuleFor, test data generation |

## npm Ecosystem (JavaScript/TypeScript)

### General Query Patterns

```
# Installation and setup
query-docs(libraryId: "/resolved-id", query: "install setup getting started quickstart")

# Configuration
query-docs(libraryId: "/resolved-id", query: "configuration options config file")

# TypeScript types
query-docs(libraryId: "/resolved-id", query: "TypeScript types generics interface")
```

## PyPI Ecosystem (Python)

### General Query Patterns

```
# Installation and basic usage
query-docs(libraryId: "/resolved-id", query: "install pip getting started basic usage")

# Configuration
query-docs(libraryId: "/resolved-id", query: "configuration settings environment variables")

# Async support
query-docs(libraryId: "/resolved-id", query: "async await asyncio support")
```

## Infrastructure Tools

### Docker

```
libraryName: "Docker"
```

| Task | Query |
|------|-------|
| Dockerfile | `"Dockerfile multi-stage build"` |
| Compose | `"docker-compose services volumes networks"` |
| Networking | `"container networking bridge host"` |

### Terraform

```
libraryName: "Terraform"
```

| Task | Query |
|------|-------|
| Provider config | `"provider configuration authentication"` |
| Resource creation | `"resource block lifecycle create"` |
| State management | `"state backend remote configuration"` |

## Tips for Unknown Libraries

When encountering an unfamiliar library:

1. **Start broad** — `resolve-library-id(libraryName: "[name]", query: "getting started overview")`
2. **Check snippet count** — Higher counts mean better documentation coverage
3. **Query setup first** — `query-docs(libraryId: "/id", query: "installation setup configuration quickstart")`
4. **Then specific needs** — `query-docs(libraryId: "/id", query: "[specific feature or API]")`

If `resolve-library-id` returns no results:
- Try alternative package names (e.g., `"redis"` instead of `"StackExchange.Redis"`)
- Try the GitHub org/repo name directly
- Fall back to web search for the library documentation
