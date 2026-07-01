# Extend the Pipeline
> Last verified against v2.0.1

The MediatR pipeline is the extension point. Add your own commands, queries, pipeline behaviours, and validators in a consumer assembly, then hand that assembly to `AddConsumerHandlers` so the framework discovers them.

```csharp
using System.Reflection;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Commands;
using MediatR;

// 1. Define a custom command — a record implementing ICommand<TResponse>.
public record PostLedgerJournalCommand(string DataAreaId, string JournalBatchNumber)
    : ICommand<Result<PostedJournalReceipt>>
{
    // ICommand carries IContext; override GetLoggingContext to enrich the structured log scope.
    public IReadOnlyDictionary<string, object> GetLoggingContext() => new Dictionary<string, object>
    {
        ["EntityType"] = nameof(LedgerJournalHeader),
        ["DataAreaId"] = DataAreaId,
        ["JournalBatchNumber"] = JournalBatchNumber,
    };
}

public sealed record PostedJournalReceipt(string Voucher, DateTime PostedAt);

// 2. Implement the handler — return Result<T>, never throw for a business failure.
public sealed class PostLedgerJournalCommandHandler
    : IRequestHandler<PostLedgerJournalCommand, Result<PostedJournalReceipt>>
{
    public async Task<Result<PostedJournalReceipt>> Handle(
        PostLedgerJournalCommand request,
        CancellationToken cancellationToken)
    {
        Result<PostedJournalReceipt> action = await CallBoundActionAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return action;
    }
}

// 3. Register the assembly in the AddIntegratoR builder.
services.AddIntegratoR(configuration, integrator =>
    integrator.AddConsumerHandlers(Assembly.GetExecutingAssembly()));

// 4. Send it through IMediator like any other command.
Result<PostedJournalReceipt> result = await mediator.Send(
    new PostLedgerJournalCommand("USMF", "B0001"),
    cancellationToken).ConfigureAwait(false);
```

`ICommand<TResponse>` is `IRequest<TResponse>` plus `IContext`. Use the non-generic `ICommand` for a command that reports only success or failure; its handler returns `Task<Result>`.

`AddConsumerHandlers` folds the assembly into `AddIntegratoR`'s single combined MediatR scan, so the framework's generic `CreateCommand<T>`/`UpdateCommand<T>`/`DeleteCommand<T>`/`GetByKeyQuery<T>`/`GetByFilterQuery<T>` handlers also close over any entity you declare there — including subclasses of framework entities — with no extra registration.

## Handle the failure path

A custom handler surfaces a D365 rejection as a failed `Result<T>` — it does not throw. Branch on `result.IsFailed` and read `result.GetError()`.

```csharp
if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    // e.g. Code "OData.RequestFailed", Type ErrorType.Failure when the bound action returns HTTP 400.
    return Results.Problem(error?.Message);
}

PostedJournalReceipt receipt = result.Value;
```

Inside the handler, wrap a rejected downstream call in an `IntegrationError` rather than letting an exception escape:

```csharp
if (action.IsFailed)
{
    return Result.Fail(new IntegrationError(
        "OData.RequestFailed",
        "The bound action returned HTTP 400.",
        ErrorType.Failure));
}
```

`ErrorType` has four members: `Failure`, `Validation`, `NotFound`, `Conflict`. See [Handle Errors](Handle-Errors) for the full mapping.

## Add a custom query

Queries follow the same shape with `IQuery<TResponse>`:

```csharp
public record GetOpenJournalCountQuery(string DataAreaId) : IQuery<Result<int>>
{
    public IReadOnlyDictionary<string, object> GetLoggingContext() =>
        new Dictionary<string, object> { ["DataAreaId"] = DataAreaId };
}
```

Pair it with `IRequestHandler<GetOpenJournalCountQuery, Result<int>>` in the consumer assembly. To cache the response, implement `ICacheableQuery<TResponse>` instead and set `CacheKey` — see [Cache Query Results](Cache-Query-Results).

## Add a custom validator

Write an `AbstractValidator<T>` for your command or query in the consumer assembly. `AddConsumerHandlers` registers it, and the `ValidationBehaviour` runs it before the handler.

```csharp
using FluentValidation;

public sealed class PostLedgerJournalCommandValidator
    : AbstractValidator<PostLedgerJournalCommand>
{
    public PostLedgerJournalCommandValidator()
    {
        RuleFor(command => command.DataAreaId).NotEmpty().Length(1, 4);
        RuleFor(command => command.JournalBatchNumber).NotEmpty();
    }
}
```

A validation failure short-circuits the pipeline with a failed `Result` carrying `IntegrationError` code `Validation.Error` and `ErrorType.Validation`. See [Add Validation](Add-Validation).

## Add a custom pipeline behaviour

Register an `IPipelineBehavior<TRequest, TResponse>` after `AddIntegratoR` and it runs after the three built-in behaviours.

```csharp
using MediatR;

public sealed class IdempotencyBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IIdempotencyStore _store; // consumer-defined; not shipped by the framework

    public IdempotencyBehaviour(IIdempotencyStore store) => _store = store;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Cross-cutting concern — record the idempotency token, then call the next step.
        return await next().ConfigureAwait(false);
    }
}
```

Register it in the same `IServiceCollection`:

```csharp
services.AddIntegratoR(configuration, integrator =>
    integrator.AddConsumerHandlers(Assembly.GetExecutingAssembly()));

services.AddTransient(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehaviour<,>));
```

## Understand the execution order

`AddIntegratoR` registers the three built-in behaviours in this order:

**Logging → Validation → Caching → Handler.**

Registration is onion-nested: the first behaviour registered wraps every later one. So `Logging` is outermost (it times and records the whole request), `Validation` short-circuits before `Caching` and the handler, and `Caching` serves a cached hit without invoking the handler.

Because `AddIntegratoR` registers all three first, a behaviour you register **after** it runs **inside** the built-ins — after `Caching`, immediately around the handler:

`Logging → Validation → Caching → YourBehaviour → Handler`

> [!CAUTION]
> Registration order fixes execution order, and getting it wrong fails silently. Register a behaviour that must reject a request (an auth or idempotency gate) **after** `AddIntegratoR` and it runs after `Caching` — a cached response is returned before your gate ever executes, so the gate looks wired but never fires. To run a behaviour before `Validation`, register it on the `IServiceCollection` **before** calling `AddIntegratoR`.

## See Also

- [Send Commands](Send-Commands) — when the generic `CreateCommand<T>`/`UpdateCommand<T>`/`DeleteCommand<T>` shape is enough
- [Add Validation](Add-Validation) — validator authoring and the `Validation.Error` failure
- [Cache Query Results](Cache-Query-Results) — opt a custom query into the caching behaviour
- [Understand the Architecture](Understand-the-Architecture) — the pipeline model and dependency direction
