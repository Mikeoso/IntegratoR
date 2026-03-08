# Write a Specialised Command

Extend the generic command pattern with domain-specific logic by creating a specialised command record and handler. This is the pattern used by the ledger journal commands.

> **Prerequisites:** [[Create-an-Entity]], [[Register-Services-in-Your-Host]]

## Define the Command Record

A specialised command is a record that wraps a generic command, constraining `TEntity` to your domain entity:

```csharp
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace MyApp.Features.Commands;

// Extends CreateCommand<TEntity> with a domain-specific constraint
public record CreateLedgerJournalHeaderCommand<TEntity>(TEntity LedgerJournalHeader)
    : CreateCommand<TEntity>(LedgerJournalHeader)
    where TEntity : LedgerJournalHeader;
```

The generic constraint `where TEntity : LedgerJournalHeader` means this command only accepts `LedgerJournalHeader` or its subclasses, giving you compile-time safety while preserving extensibility.

For commands that need custom logging context instead of inheriting from a generic base, implement `ICommand<TResponse>` directly:

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace MyApp.Features.Commands;

public record CreateLedgerJournalLineCommand<TEntity>(TEntity LedgerJournalLine)
    : ICommand<Result<TEntity>>
    where TEntity : LedgerJournalLine
{
    public IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return LedgerJournalLine.GetLoggingContext();
    }
}
```

## Create the Handler

The handler adds domain-specific logging and delegates to `IService<TEntity>`:

```csharp
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MyApp.Features.Commands;

public class CreateLedgerJournalHeaderHandler<TEntity>(
    ILogger<CreateLedgerJournalHeaderHandler<TEntity>> logger,
    IService<TEntity> service)
    : IRequestHandler<CreateLedgerJournalHeaderCommand<TEntity>, Result<TEntity>>
    where TEntity : LedgerJournalHeader
{
    private readonly ILogger<CreateLedgerJournalHeaderHandler<TEntity>> _logger = logger;
    private readonly IService<TEntity> _service = service;

    public async Task<Result<TEntity>> Handle(
        CreateLedgerJournalHeaderCommand<TEntity> request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating Ledger Journal Header with Journal Name: {JournalName} in Company: {Company}",
            request.LedgerJournalHeader.JournalName,
            request.LedgerJournalHeader.DataAreaId);

        var addResult = await _service.AddAsync(
            request.LedgerJournalHeader, cancellationToken).ConfigureAwait(false);

        return addResult.Match(
            onSuccess: entity =>
            {
                _logger.LogInformation(
                    "Created Journal Header {JournalBatchNumber} in {Company}",
                    entity.JournalBatchNumber,
                    entity.DataAreaId);

                return Result.Ok(entity);
            },
            onFailure: error =>
            {
                return Result.Fail<TEntity>(error);
            });
    }
}
```

## Understand the Match Pattern

The `Match` extension on `Result<T>` provides a clean way to handle success and failure branches:

```csharp
var addResult = await _service.AddAsync(entity, cancellationToken).ConfigureAwait(false);
// Result: Result<TEntity> — Success with created entity or Failure with IntegrationError

// Match forces you to handle both outcomes
return addResult.Match(
    onSuccess: entity =>
    {
        // Domain-specific success logic (logging, enrichment, etc.)
        return Result.Ok(entity);
    },
    onFailure: error =>
    {
        // Domain-specific failure logic (logging, error wrapping, etc.)
        return Result.Fail<TEntity>(error);
    });
```

An alternative approach without `Match` uses explicit `IsFailed` checks, as seen in the update handlers:

```csharp
var updateResult = await _service.UpdateAsync(
    request.LedgerJournalHeader, cancellationToken).ConfigureAwait(false);
// Result: Result<TEntity> — Success with updated entity or Failure

if (updateResult.IsFailed)
{
    return Result.Fail<TEntity>(updateResult.Errors);
}

return Result.Ok(updateResult.Value!);
```

## Define a Batch Variant

For bulk operations, extend `CreateBatchCommand<TEntity>` and override the logging context:

```csharp
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace MyApp.Features.Commands;

public record CreateLedgerJournalHeadersCommand<TEntity>(
    IEnumerable<TEntity> LedgerJournalHeaders)
    : CreateBatchCommand<TEntity>(LedgerJournalHeaders)
    where TEntity : LedgerJournalHeader
{
    public override IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return new Dictionary<string, object>
        {
            { "EntityType", typeof(TEntity).Name },
            { "Count", LedgerJournalHeaders.Count() },
            { "JournalNames", string.Join(", ", LedgerJournalHeaders.Select(j => j.JournalName)) }
        };
    }
}
```

## When Things Go Wrong

If you forget to register the handler in DI, MediatR throws an exception when you send the command:

```text
System.InvalidOperationException: No service for type
'IRequestHandler<CreateLedgerJournalHeaderCommand<LedgerJournalHeader>, Result<LedgerJournalHeader>>'
has been registered.
```

Ensure your handler assembly is registered in the MediatR configuration:

```csharp
services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CreateLedgerJournalHeaderHandler<>).Assembly));
```

If your entity constraint is wrong (e.g. the command accepts `BaseEntity<string>` but the handler constrains to `LedgerJournalHeader`), you get a compile-time error rather than a runtime failure.

## See Also

- [[Create-a-Ledger-Journal]] — see specialised commands in action
- [[Create-an-Entity]] — the generic command these extend
- [[Send-Your-First-Command]] — MediatR pipeline basics
