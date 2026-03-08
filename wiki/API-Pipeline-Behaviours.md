# Pipeline Behaviours

Three MediatR pipeline behaviours that provide cross-cutting concerns for every command and query. They execute in a fixed order: Logging, then Validation, then Caching, then the Handler.

## Use the Behaviours

```csharp
// Behaviours are registered automatically by AddApplicationServices()
services.AddApplicationServices();

// Every mediator.Send() call flows through all three behaviours:
// LoggingBehaviour -> ValidationBehaviour -> CachingBehaviour -> Handler
Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(entity),
    cancellationToken);
```

## Pipeline Order

```
Request
  |
  v
LoggingBehaviour    -- Logs start, measures time, logs outcome
  |
  v
ValidationBehaviour -- Runs FluentValidation, short-circuits on failure
  |
  v
CachingBehaviour    -- Cache lookup (queries only), short-circuits on hit
  |
  v
Handler             -- Executes business logic
  |
  v
CachingBehaviour    -- Stores successful result in cache
  |
  v
LoggingBehaviour    -- Logs elapsed time and result status
  |
  v
Response
```

## LoggingBehaviour\<TRequest, TResponse\>

Wraps every request with structured logging and performance timing.

```csharp
public class LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IContext
```

### Behaviour

| Event | Log Level | What is logged |
|-------|-----------|----------------|
| Request start | Information | Request name and serialised request data |
| Successful completion | Information | Request name and elapsed milliseconds |
| Result failure (`Result.IsFailed`) | Warning | Request name, elapsed time, error code and message |
| Unhandled exception | Error | Request name, elapsed time, exception details |
| Response details | Debug | Full serialised response object |

### Structured logging scope

Uses `IContext.GetLoggingContext()` to create a logging scope, enriching all log entries with the request's context properties:

```
// Log output for CreateCommand<LedgerJournalHeader>:
// [Information] Handling CreateCommand`1 with data: {@Request}
//   Scope: { JournalBatchNumber: "JBN-001", DataAreaId: "USMF", Description: "Accruals" }
// [Information] Handled CreateCommand`1 successfully in 234ms
```

## ValidationBehaviour\<TRequest, TResponse\>

Runs all registered `IValidator<TRequest>` instances and short-circuits with an `IntegrationError` on failure.

```csharp
public class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResultBase
```

### Behaviour

1. If no validators are registered for the request type, passes through immediately.
2. Executes all validators and collects failures.
3. On failure: returns the first validation error as `IntegrationError("Validation.Error", message, ErrorType.Validation)`.
4. On success: calls the next behaviour in the pipeline.

### Short-circuit example

```csharp
// Validator
public class CreateCommandValidator : AbstractValidator<CreateCommand<LedgerJournalHeader>>
{
    public CreateCommandValidator()
    {
        RuleFor(c => c.Entity.DataAreaId).NotEmpty();
        RuleFor(c => c.Entity.JournalBatchNumber).NotEmpty();
    }
}

// Sending an invalid command
var invalid = new LedgerJournalHeader
{
    JournalBatchNumber = "",  // Empty -- fails validation
    DataAreaId = "USMF"
};

Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(invalid),
    cancellationToken);

result.IsFailed;              // true
result.GetError()?.Code;      // "Validation.Error"
result.GetError()?.Type;      // ErrorType.Validation
result.GetError()?.Message;   // "'JournalBatchNumber' must not be empty"
// Handler was never called -- pipeline was short-circuited
```

### Handling both Result types

The behaviour handles both `Result<T>` (generic) and `Result` (non-generic) through reflection, dynamically constructing the correct failed result type.

## CachingBehaviour\<TRequest, TResponse\>

Transparent caching for queries that implement `ICacheableQuery<TResponse>`. Non-cacheable requests pass through without any overhead.

```csharp
public class CachingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResultBase
```

### Behaviour

1. If the request does not implement `ICacheableQuery<TResponse>`, passes through immediately.
2. Checks the cache using the query's `CacheKey`.
3. Cache hit: returns the cached response, logs at Debug level.
4. Cache miss: executes the handler. If the result is successful, caches it with the specified `CacheDuration`.
5. Failed results are never cached.

### Cache flow example

```csharp
// First call: cache MISS
var query = new GetFinancialDimensionsQuery("USMF");
Result<IEnumerable<FinancialDimension>> result1 = await mediator.Send(query, cancellationToken);
// Debug: Cache MISS for key DimensionOrders:[USMF]. Executing handler.
// Debug: Handler executed successfully. Caching response...

// Second call: cache HIT
Result<IEnumerable<FinancialDimension>> result2 = await mediator.Send(query, cancellationToken);
// Debug: Cache HIT for key DimensionOrders:[USMF]. Returning cached response.
// Handler is NOT called
```

## Adding a Custom Behaviour

Register custom behaviours in your DI setup. They execute in registration order relative to the existing behaviours.

```csharp
// Custom behaviour
public class TransactionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Pre-handler logic
        var response = await next().ConfigureAwait(false);
        // Post-handler logic
        return response;
    }
}

// Register after the built-in behaviours
services.AddApplicationServices();
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehaviour<,>));
// Pipeline: Logging -> Validation -> Caching -> Transaction -> Handler
```

## Register the Behaviours

All three behaviours are registered by `AddApplicationServices()`:

```csharp
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehaviour<,>));
```

Registration order defines execution order. Do not reorder these registrations.

## See Also

- [[API-AddApplicationServices]] — registers behaviours in the correct pipeline order
- [[API-ICacheableQuery]] — caching contract used by the cache behaviour
- [[API-IntegrationError]] — error type returned by the validation behaviour
- [[API-ICommand]] — command interface that behaviours intercept
