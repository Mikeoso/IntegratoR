using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;

/// <summary>Creates a batch of LedgerJournalLine entities in D365 F&amp;O, emitting domain-specific structured logs. Retained over the generic CreateBatchCommandHandler&lt;T&gt; because it adds journal-context (Count) logging.</summary>
public class CreateLedgerJournalLinesHandler<TEntity> : IRequestHandler<CreateLedgerJournalLinesCommand<TEntity>, Result> where TEntity : LedgerJournalLine
{
    private readonly ILogger<CreateLedgerJournalLinesHandler<TEntity>> _logger;
    private readonly IODataBatchService<TEntity> _service;

    /// <summary>Initializes a new instance of the <see cref="CreateLedgerJournalLinesHandler{TEntity}"/> class.</summary>
    public CreateLedgerJournalLinesHandler(ILogger<CreateLedgerJournalLinesHandler<TEntity>> logger, IODataBatchService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    /// <inheritdoc/>
    public async Task<Result> Handle(CreateLedgerJournalLinesCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating {Count} Ledger Journal Lines in F&O.", request.LedgerJournalLines.Count);

        var addResult = await _service.AddBatchAsync(request.LedgerJournalLines, cancellationToken).ConfigureAwait(false);

        return addResult.Match(
            onSuccess: () =>
            {
                _logger.LogInformation("Successfully created {Count} Ledger Journal Lines in F&O.", request.LedgerJournalLines.Count);
                return Result.Ok();
            },
            onFailure: error =>
            {
                _logger.LogError("Failed to create Ledger Journal Lines: {Error}", error.Message);
                return Result.Fail(error);
            });
    }
}
