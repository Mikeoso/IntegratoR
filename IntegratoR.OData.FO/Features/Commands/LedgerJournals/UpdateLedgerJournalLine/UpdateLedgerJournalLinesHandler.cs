using FluentResults;
using IntegratoR.Abstractions.Common.Batch;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalLine
{
    /// <summary>Updates a batch of LedgerJournalLine entities in D365 F&amp;O, emitting domain-specific structured logs. Retained over the generic UpdateBatchCommandHandler&lt;T&gt; because it adds journal-context (Count) logging.</summary>
    /// <typeparam name="TEntity">The type of the entity being updated.</typeparam>
    public class UpdateLedgerJournalLinesHandler<TEntity> : IRequestHandler<UpdateLedgerJournalLinesCommand<TEntity>, Result<BatchOutcome>> where TEntity : LedgerJournalLine
    {
        private readonly ILogger<UpdateLedgerJournalLinesHandler<TEntity>> _logger;
        private readonly IODataBatchService<TEntity> _service;

        /// <summary>Initializes a new instance of the <see cref="UpdateLedgerJournalLinesHandler{TEntity}"/> class.</summary>
        public UpdateLedgerJournalLinesHandler(ILogger<UpdateLedgerJournalLinesHandler<TEntity>> logger, IODataBatchService<TEntity> service)
        {
            _logger = logger;
            _service = service;
        }

        /// <inheritdoc/>
        public async Task<Result<BatchOutcome>> Handle(UpdateLedgerJournalLinesCommand<TEntity> request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Updating Ledger Journal Lines in batch...");

            Result<BatchOutcome> result = await _service.UpdateBatchAsync(request.LedgerJournalLines, request.Options, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                _logger.LogInformation("Successfully updated {Count} Ledger Journal Lines.", request.LedgerJournalLines.Count);
            }
            return result;
        }
    }
}
