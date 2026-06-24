# Extend the Pipeline

The MediatR pipeline is the central extension point. Custom commands, custom queries, and custom pipeline behaviours all plug in without modifying framework code.

## When to Add a Custom Command

The generic `CreateCommand<T>` / `UpdateCommand<T>` / `DeleteCommand<T>` shape covers the common CRUD cases. Reach for a custom command when:

- The operation invokes a D365 OData **bound action** (e.g. post a journal, release a sales order).
- Multiple service calls need to be composed atomically.
- Entity-specific logging context or pre-validation logic must run before the underlying CRUD operation.
- The wire payload shape differs from the entity's serialised form (e.g. needs a wrapping envelope).

## Define a Custom Command

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Commands;

public record PostLedgerJournalCommand(
    string DataAreaId,
    string JournalBatchNumber)
    : ICommand<Result<PostedJournalReceipt>>;

public sealed record PostedJournalReceipt(string Voucher, DateTime PostedAt);
```

`ICommand<TResponse>` is `IRequest<TResponse> + IContext`. `IContext` carries the `GetLoggingContext()` method that the `LoggingBehaviour` uses for structured-log enrichment — overriding it lets a command emit additional structured properties:

```csharp
public record PostLedgerJournalCommand(
    string DataAreaId,
    string JournalBatchNumber)
    : ICommand<Result<PostedJournalReceipt>>
{
    public IReadOnlyDictionary<string, object> GetLoggingContext() => new Dictionary<string, object>
    {
        ["EntityType"] = nameof(LedgerJournalHeader),
        ["DataAreaId"] = DataAreaId,
        ["JournalBatchNumber"] = JournalBatchNumber
    };
}
```

For a fire-and-forget command (no return value), use the non-generic `ICommand`:

```csharp
public record ArchiveJournalCommand(string DataAreaId, string JournalBatchNumber)
    : ICommand;
```

The handler returns `Task<Result>` rather than `Task<Result<T>>`.

## Implement the Handler

```csharp
public sealed class PostLedgerJournalCommandHandler
    : IRequestHandler<PostLedgerJournalCommand, Result<PostedJournalReceipt>>
{
    private readonly IODataClientAdapter _adapter;
    private readonly ILogger<PostLedgerJournalCommandHandler> _logger;

    public PostLedgerJournalCommandHandler(
        IODataClientAdapter adapter,
        ILogger<PostLedgerJournalCommandHandler> logger)
    {
        _adapter = adapter;
        _logger = logger;
    }

    public async Task<Result<PostedJournalReceipt>> Handle(
        PostLedgerJournalCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Posting journal {DataAreaId}/{BatchNumber}",
            request.DataAreaId, request.JournalBatchNumber);

        // Compose the OData bound action call, deserialise the receipt,
        // and return Result<T>. Exceptions are caught and wrapped as IntegrationError.
        Result<PostedJournalReceipt> actionResult = await CallBoundActionAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (actionResult.IsFailed)
        {
            // The bound action rejected the request — surface a typed failure, never throw.
            return Result.Fail(new IntegrationError(
                "OData.RequestFailed",
                "The bound action returned HTTP 400.",
                ErrorType.Failure));
        }

        return actionResult;
    }
}
```

The handler's assembly must be passed to `AddConsumerHandlers(...)` so MediatR discovers it:

```csharp
services.AddIntegratoR(configuration, integrator =>
{
    integrator.AddConsumerHandlers(Assembly.GetExecutingAssembly());
});
```

A custom command can be invoked through `IMediator.Send(...)` like any other:

```csharp
Result<PostedJournalReceipt> result = await mediator.Send(
    new PostLedgerJournalCommand("USMF", "JBN-000431"),
    cancellationToken).ConfigureAwait(false);
```

## Add a Custom Pipeline Behaviour

The three built-in behaviours are registered inside `AddApplicationServices` in the canonical order **Logging → Validation → Caching → Handler**. Custom behaviours register alongside them via standard MediatR DI:

```csharp
using FluentResults;
using MediatR;

public sealed class IdempotencyBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResultBase
{
    private readonly IIdempotencyStore _store;  // consumer-defined type — not shipped by the framework

    public IdempotencyBehaviour(IIdempotencyStore store)
    {
        _store = store;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Custom cross-cutting concern — check + record idempotency token, then call next()
        return await next().ConfigureAwait(false);
    }
}
```

Register the behaviour in the consumer's DI wiring:

```csharp
services.AddTransient(
    typeof(IPipelineBehavior<,>),
    typeof(IdempotencyBehaviour<,>));
```

> The order behaviours execute depends on registration order. Behaviours registered **after** `AddIntegratoR` run **after** the built-in behaviours (Logging → Validation → Caching → Custom → Handler). To run a custom behaviour before validation, register it before calling `AddIntegratoR` — note that this requires manual MediatR registration since `AddIntegratoR` calls `AddApplicationServices` which sets the default chain.

## Custom Query

Custom queries follow the same pattern with `IQuery<TResponse>`:

```csharp
public record GetOpenJournalCountQuery(string DataAreaId)
    : IQuery<Result<int>>;
```

Pair with `IRequestHandler<GetOpenJournalCountQuery, Result<int>>` in the consumer assembly. Make the query implement `ICacheableQuery<TResponse>` to opt into the cache layer — see [Cache Query Results](Cache-Query-Results).

## Custom Validators

Custom validators for custom commands or queries are discovered by `AddConsumerHandlers(...)` automatically. See [Add Validation](Add-Validation) for the validator authoring pattern.

## Use the Built-In Services Directly

Sometimes the cleanest extension is **not** a new command but a direct call to `IService<T>` from a custom service or handler:

```csharp
public sealed class JournalReconciliationService
{
    private readonly IService<LedgerJournalHeader> _service;

    public JournalReconciliationService(IService<LedgerJournalHeader> service)
    {
        _service = service;
    }

    public async Task<Result<int>> CountUnpostedAsync(
        string dataAreaId,
        CancellationToken cancellationToken)
    {
        Result<IEnumerable<LedgerJournalHeader>> result = await _service.FindAsync(
            h => h.DataAreaId == dataAreaId && h.IsPosted == NoYes.No,
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Result.Ok(result.Value.Count())
            : Result.Fail<int>(result.Errors);
    }
}
```

`IService<TEntity>` is registered transparently by `AddIntegratoR` for every `IEntity` type — both built-in F&O entities and consumer-defined entities (provided the entity's assembly is reachable). The methods on `IService<T>` mirror the generic command and query handlers.

The same pattern works for `IODataBatchService<T>` (bulk operations) and `IODataService<T>` (the typed PanoramicData-flavoured surface). All three are registered against the same concrete `ODataService<T>` implementation.

## See Also

- [Send Commands](Send-Commands) — when the generic shape is sufficient
- [Run Queries](Run-Queries) — when a custom query reads better than a long LINQ expression
- [Add Validation](Add-Validation) — validators for custom commands and queries
- [Cache Query Results](Cache-Query-Results) — opt custom queries into the cache layer
