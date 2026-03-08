# Extending the Pipeline

```csharp
// Every mediator.Send() flows through: Logging -> Validation -> Caching -> Handler
services.AddApplicationServices(); // registers all three behaviours + MediatR + validators

Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(entity), cancellationToken);
```

## Pipeline Behaviour Order

Registration order in [[Getting-Started]] defines execution order:

```csharp
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));   // 1. Logs request/response, measures elapsed time
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>)); // 2. Runs FluentValidation, short-circuits on failure
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehaviour<,>));    // 3. Cache lookup (queries only), short-circuits on hit
// 4. Handler executes business logic
```

If logging ran after validation, failed requests would not be logged. If caching ran before validation, invalid requests could return cached results. If caching ran before the handler, every request would bypass the cache.

## Behaviours

**`LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>, IContext`** — wraps the entire pipeline. Uses `IContext.GetLoggingContext()` for structured logging scope. Logs at Information (start/success), Warning (result failure), Error (exception), Debug (response details).

**`ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TResponse : IResultBase`** — runs all registered `IValidator<TRequest>` instances. On failure, returns `IntegrationError("Validation.Error", message, ErrorType.Validation)` without calling the handler.

```csharp
var invalid = new LedgerJournalHeader { JournalBatchNumber = "", DataAreaId = "USMF" };
Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(invalid), cancellationToken);
result.IsFailed;            // true
result.GetError()?.Code;    // "Validation.Error"
result.GetError()?.Type;    // ErrorType.Validation — handler never called
```

**`CachingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TResponse : IResultBase`** — transparent caching for requests implementing `ICacheableQuery<TResponse>`. Cache hit returns immediately; cache miss executes the handler and caches successful results. Failed results are never cached.

## Adding a Custom Behaviour

Insert at the appropriate position relative to existing behaviours:

```csharp
public class AuthorizationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Pre-handler logic
        var response = await next().ConfigureAwait(false);
        // Post-handler logic
        return response;
    }
}

// Register after built-in behaviours
services.AddApplicationServices();
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehaviour<,>));
// Pipeline: Logging -> Validation -> Caching -> Authorization -> Handler
```

Or insert between built-in behaviours by registering them manually:

```csharp
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehaviour<,>)); // custom
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehaviour<,>));
```

## Writing a Specialised Command

Wrap a generic command with a domain-specific constraint:

```csharp
public record CreateLedgerJournalHeaderCommand<TEntity>(TEntity LedgerJournalHeader)
    : CreateCommand<TEntity>(LedgerJournalHeader)
    where TEntity : LedgerJournalHeader;
```

For custom logging context, implement `ICommand<TResponse>` directly:

```csharp
public record CreateLedgerJournalLineCommand<TEntity>(TEntity LedgerJournalLine)
    : ICommand<Result<TEntity>>
    where TEntity : LedgerJournalLine
{
    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => LedgerJournalLine.GetLoggingContext();
}
```

The handler adds domain-specific logic and delegates to [[Commands|`IService<TEntity>`]]:

```csharp
public class CreateLedgerJournalHeaderHandler<TEntity>(
    ILogger<CreateLedgerJournalHeaderHandler<TEntity>> logger,
    IService<TEntity> service)
    : IRequestHandler<CreateLedgerJournalHeaderCommand<TEntity>, Result<TEntity>>
    where TEntity : LedgerJournalHeader
{
    public async Task<Result<TEntity>> Handle(
        CreateLedgerJournalHeaderCommand<TEntity> request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating Journal Header {JournalName} in {Company}",
            request.LedgerJournalHeader.JournalName, request.LedgerJournalHeader.DataAreaId);

        var addResult = await service.AddAsync(
            request.LedgerJournalHeader, cancellationToken).ConfigureAwait(false);

        return addResult.Match(
            onSuccess: entity => Result.Ok(entity),     // Match forces you to handle both outcomes
            onFailure: error => Result.Fail<TEntity>(error));
    }
}
```

For bulk operations, extend `CreateBatchCommand<TEntity>`:

```csharp
public record CreateLedgerJournalHeadersCommand<TEntity>(
    IEnumerable<TEntity> LedgerJournalHeaders)
    : CreateBatchCommand<TEntity>(LedgerJournalHeaders)
    where TEntity : LedgerJournalHeader
{
    public override IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object>
        {
            { "EntityType", typeof(TEntity).Name },
            { "Count", LedgerJournalHeaders.Count() },
            { "JournalNames", string.Join(", ", LedgerJournalHeaders.Select(j => j.JournalName)) }
        };
}
```

## AddApplicationServices Registration

`AddApplicationServices()` (`public static IServiceCollection AddApplicationServices(this IServiceCollection services)`) registers the full Application layer. Call it before layer-specific registrations since OData, OData.FO, and RELion depend on MediatR and the pipeline:

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationServices();                    // pipeline + handlers + validators + cache + auth
        services.AddODataClient(context.Configuration);       // OData layer
        services.AddODataClientFOProxy(context.Configuration); // F&O layer (registers its own validators)
    })
    .Build();
```

Registered services: `ICacheService` -> `InMemoryCacheService` (singleton), `IAuthenticator` -> `OAuthAuthenticator` (singleton), `IMemoryCache` (singleton). MediatR is configured with `RegisterGenericHandlers = true` for generic CQRS handler resolution. Validators are assembly-scanned including internal types.

To replace the default in-memory cache for scaled-out scenarios, register after `AddApplicationServices()`:

```csharp
services.AddApplicationServices();
services.AddSingleton<ICacheService, RedisCacheService>(); // overrides InMemoryCacheService
```
