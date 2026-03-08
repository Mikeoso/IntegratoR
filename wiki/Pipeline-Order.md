# Pipeline Order

The MediatR pipeline processes every command and query through a chain of behaviours before reaching the handler. The order of registration determines the execution order and is critical for correct operation.

> **Prerequisites:** [[Architecture-Overview]], [[Register-Services-in-Your-Host]]

## Pipeline Flow

```
Request In
    |
    v
+-------------------+
| LoggingBehaviour   |  1. Logs request/response, measures elapsed time
+--------+----------+
         |
         v
+-------------------+
| ValidationBehaviour|  2. Runs FluentValidation validators, fails fast on invalid input
+--------+----------+
         |
         v
+-------------------+
| CachingBehaviour   |  3. Returns cached result if available, caches response after handler
+--------+----------+
         |
         v
+-------------------+
|     Handler        |  4. Executes the actual command or query logic
+-------------------+
         |
         v
Response Out (bubbles back up through each behaviour)
```

## Register the Behaviours

From `AddApplicationServices()` in `IntegratoR.Application`:

```csharp
public static IServiceCollection AddApplicationServices(this IServiceCollection services)
{
    // Pipeline behaviours - ORDER MATTERS
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehaviour<,>));

    // MediatR handlers
    services.AddMediatR(cfg =>
    {
        cfg.RegisterGenericHandlers = true;
        cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    });

    // FluentValidation validators
    services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true);

    // Core services
    services.AddSingleton<ICacheService, InMemoryCacheService>();
    services.AddSingleton<IAuthenticator, OAuthAuthenticator>();
    services.AddMemoryCache();

    return services;
}
```

## Why Order Matters

### 1. LoggingBehaviour (outermost)

Wraps the entire pipeline to capture total elapsed time and log both the incoming request and the final response. Registered first so it sees everything, including validation failures and cache hits.

```
[14:30:00 INF] Handling CreateCommand<LedgerJournalHeader>
[14:30:01 INF] Handled CreateCommand<LedgerJournalHeader> in 1042ms
```

If logging were registered after validation, failed requests would not be logged.

### 2. ValidationBehaviour (fail fast)

Runs all registered `IValidator<TRequest>` validators before the request reaches the cache or handler. Invalid requests are rejected immediately without wasting cache lookups or API calls.

```csharp
// If validation fails, the pipeline short-circuits here
// Returns Result.Fail with ErrorType.Validation
// The handler never executes
```

If validation ran after caching, invalid requests could return cached (valid) results, masking the validation error.

### 3. CachingBehaviour (performance)

Checks the in-memory cache before calling the handler. If a cached result exists, returns it immediately. After a cache miss, the handler executes and the result is cached for subsequent requests.

Only applies to requests that implement the caching interface. Commands are not cached -- only queries.

```
Cache HIT  -> return cached result (handler never runs)
Cache MISS -> call handler -> cache result -> return result
```

If caching ran before validation, invalid requests could pollute the cache with error results.

### 4. Handler (innermost)

The actual command or query handler that performs business logic, calls services, and returns results.

## Swapping the Order: What Goes Wrong

| Incorrect Order | Problem |
|----------------|---------|
| Validation before Logging | Failed validation is not logged |
| Caching before Validation | Invalid requests get cached results |
| Handler before Caching | Cache is never checked, every request hits the service |
| Logging in the middle | Elapsed time only measures inner behaviours |

## Adding Custom Behaviours

Register additional behaviours by inserting them at the appropriate position:

```csharp
// Example: adding an authorization behaviour after validation
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehaviour<,>)); // custom
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehaviour<,>));
```

## Spelling Note

The codebase uses British spelling throughout: `Behaviour` not `Behavior`. This is intentional and consistent across all behaviour classes.

## See Also

- [[Architecture-Overview]] — layer structure and dependency flow
- [[Add-Validation-to-a-Command]] — how to add validators
- [[Cache-Query-Results]] — how caching behaviour works
