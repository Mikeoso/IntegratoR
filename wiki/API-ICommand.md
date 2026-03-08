# ICommand

Marker interfaces for CQRS write operations. All commands flow through the MediatR pipeline and return `FluentResults.Result` types.

## Use the Interface

```csharp
// Command that returns a value on success
public record PostInvoiceCommand(string InvoiceId) : ICommand<Result<string>>
{
    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object> { { "InvoiceId", InvoiceId } };
}

// Command that returns only success/failure
public record TriggerSyncCommand(string DataAreaId) : ICommand
{
    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object> { { "DataAreaId", DataAreaId } };
}
```

## Interfaces

### ICommand\<TResponse\>

A command that modifies system state and returns a response payload.

```csharp
public interface ICommand<out TResponse> : IRequest<TResponse>, IContext { }
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `TResponse` | Type parameter | The response type, typically `Result<T>` |

### ICommand

A command that modifies system state and returns only success or failure.

```csharp
public interface ICommand : IRequest<Result>, IContext { }
```

Returns `Result` (non-generic) -- the handler indicates success with `Result.Ok()` or failure with `Result.Fail()`.

## See Examples

### Command with response value

```csharp
public record CreateJournalCommand(string DataAreaId, string Description)
    : ICommand<Result<string>>
{
    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object>
        {
            { "DataAreaId", DataAreaId },
            { "Description", Description }
        };
}

// Handler
public class CreateJournalHandler : IRequestHandler<CreateJournalCommand, Result<string>>
{
    public async Task<Result<string>> Handle(
        CreateJournalCommand request, CancellationToken cancellationToken)
    {
        // ... create journal in D365 F&O
        return Result.Ok("JBN-000123");
    }
}

// Sending
Result<string> result = await mediator.Send(new CreateJournalCommand("USMF", "Monthly accruals"), cancellationToken);
// result.Value == "JBN-000123"
```

### Fire-and-forget command

```csharp
public record PostJournalCommand(string JournalBatchNumber) : ICommand
{
    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object> { { "JournalBatchNumber", JournalBatchNumber } };
}

// Handler
public class PostJournalHandler : IRequestHandler<PostJournalCommand, Result>
{
    public async Task<Result> Handle(
        PostJournalCommand request, CancellationToken cancellationToken)
    {
        // ... trigger OData action
        return Result.Ok();
    }
}

// Sending
Result result = await mediator.Send(new PostJournalCommand("JBN-000123"), cancellationToken);
// result.IsSuccess == true
```

### Error handling

```csharp
Result<string> result = await mediator.Send(command, cancellationToken);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    Console.WriteLine($"[{error?.Code}] {error?.Message}");
    // Output: [D365.PostFailed] Journal could not be posted: validation errors exist
}
```

## Keep in Mind

- Both interfaces extend `IContext`, requiring `GetLoggingContext()` for structured logging in the `LoggingBehaviour`.
- Prefer `ICommand<Result<T>>` when the caller needs data back (e.g. a newly created ID).
- Prefer `ICommand` (non-generic) for operations where only success/failure matters.
- Use the pre-built [[API-Generic-Commands]] for standard CRUD instead of writing custom commands.

## See Also

- [[API-IQuery]] — companion interface for read operations
- [[API-Generic-Commands]] — built-in command implementations
- [[API-Pipeline-Behaviours]] — behaviours that intercept commands
- [[API-IntegrationError]] — error type returned on command failure
