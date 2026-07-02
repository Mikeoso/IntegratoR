using FluentResults;
using IntegratoR.Abstractions.Common.Batch;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalHeader;

/// <summary>Updates a batch of LedgerJournalHeader entities in D365 F&amp;O, emitting domain-specific structured logs. Retained over the generic UpdateBatchCommandHandler&lt;T&gt; because it adds journal-context (Count) logging.</summary>
/// <typeparam name="TEntity">The type of the entity being updated.</typeparam>
public class UpdateLedgerJournalHeadersHandler<TEntity> : IRequestHandler<UpdateLedgerJournalHeadersCommand<TEntity>, Result<BatchOutcome>> where TEntity : LedgerJournalHeader
{
    private readonly ILogger<UpdateLedgerJournalHeadersHandler<TEntity>> _logger;
    private readonly IODataBatchService<TEntity> _service;

    /// <summary>Initializes a new instance of the <see cref="UpdateLedgerJournalHeadersHandler{TEntity}"/> class.</summary>
    public UpdateLedgerJournalHeadersHandler(ILogger<UpdateLedgerJournalHeadersHandler<TEntity>> logger, IODataBatchService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    /// <inheritdoc/>
    public async Task<Result<BatchOutcome>> Handle(UpdateLedgerJournalHeadersCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating Ledger Journal Headers in batch...");

        Result<BatchOutcome> result = await _service.UpdateBatchAsync(request.LedgerJournalHeaders, request.Options, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            _logger.LogInformation("Successfully updated {Count} Ledger Journal Headers.", request.LedgerJournalHeaders.Count);
        }
        return result;
    }
}
