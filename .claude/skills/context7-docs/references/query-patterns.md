# Context7 Query Patterns

## IntegratoR Project Libraries

### MediatR (CQRS)
```
libraryName: "MediatR"
```
| Task | Query |
|------|-------|
| Pipeline behaviours | `"IPipelineBehavior request response pipeline"` |
| Notification handlers | `"INotificationHandler publish notification"` |
| DI registration | `"AddMediatR ServiceCollection registration"` |

### FluentResults
```
libraryName: "FluentResults"
```
| Task | Query |
|------|-------|
| Creating results | `"Result.Ok Result.Fail creating results"` |
| Custom errors | `"custom error class extending IError"` |
| Merging results | `"Merge results combine multiple Result"` |

### FluentValidation
```
libraryName: "FluentValidation"
```
| Task | Query |
|------|-------|
| Validator creation | `"AbstractValidator RuleFor validation rules"` |
| Async validation | `"MustAsync custom async validation"` |
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

### PanoramicData.OData.Client
```
libraryName: "PanoramicData.OData.Client"
```
| Task | Query |
|------|-------|
| CRUD operations | `"CreateAsync GetByKeyAsync UpdateAsync DeleteAsync CRUD"` |
| Batch operations | `"CreateBatch CreateChangeset batch atomic operations"` |
| Filtering (LINQ) | `"Filter LINQ expression strongly typed filtering"` |
| Query builder | `"For Select Expand OrderBy Skip Top Count query"` |
| Exception handling | `"ODataNotFoundException ODataClientException typed exceptions"` |
| Configuration | `"ODataClientOptions BaseUrl ConfigureRequest JsonSerializerOptions"` |

### StackExchange.Redis
```
libraryName: "StackExchange.Redis"
```
| Task | Query |
|------|-------|
| Connection setup | `"ConnectionMultiplexer Connect configuration"` |
| String operations | `"StringSet StringGet cache operations"` |
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

## NuGet Ecosystem

Common libraries for reference:

| Library | libraryName | Focus |
|---------|-------------|-------|
| AutoMapper | `"AutoMapper"` | Profile creation, DI, projection |
| Serilog | `"Serilog"` | Sink configuration, enrichers |
| xUnit | `"xUnit"` | Theory, InlineData, fixtures |
| NSubstitute | `"NSubstitute"` | Returns, Received, Arg matching |

## Tips for Unknown Libraries

1. **Start broad** — `resolve-library-id(libraryName: "[name]", query: "getting started overview")`
2. **Check snippet count** — Higher counts mean better documentation coverage
3. **Query setup first** — `query-docs(libraryId: "/id", query: "installation setup configuration quickstart")`
4. **Then specific needs** — `query-docs(libraryId: "/id", query: "[specific feature or API]")`

If `resolve-library-id` returns no results, try alternative package names or fall back to web search.
