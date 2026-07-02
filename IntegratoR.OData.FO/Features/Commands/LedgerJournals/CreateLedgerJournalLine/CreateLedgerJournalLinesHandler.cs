using FluentResults;
using IntegratoR.Abstractions.Common.Batch;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;

/// <summary>Creates a batch of LedgerJournalLine entities in D365 F&amp;O, emitting domain-specific structured logs. Retained over the generic CreateBatchCommandHandler&lt;T&gt; because it adds journal-context (Count) logging.</summary>
/// <typeparam name="TEntity">The type of the entity being created.</typeparam>
public class CreateLedgerJournalLinesHandler<TEntity> : IRequestHandler<CreateLedgerJournalLinesCommand<TEntity>, Result<BatchOutcome>> where TEntity : LedgerJournalLine
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
    public async Task<Result<BatchOutcome>> Handle(CreateLedgerJournalLinesCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating {Count} Ledger Journal Lines in F&O.", request.LedgerJournalLines.Count);

        Result<BatchOutcome> result = await _service.AddBatchAsync(request.LedgerJournalLines, request.Options, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            _logger.LogInformation("Successfully created {Count} Ledger Journal Lines in F&O.", request.LedgerJournalLines.Count);
        }
        return result;
    }
}
