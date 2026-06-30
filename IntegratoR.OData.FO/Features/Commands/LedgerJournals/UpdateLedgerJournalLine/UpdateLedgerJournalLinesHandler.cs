using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalLine
{
    /// <summary>Updates a batch of LedgerJournalLine entities in D365 F&amp;O, emitting domain-specific structured logs. Retained over the generic UpdateBatchCommandHandler&lt;T&gt; because it adds journal-context (Count) logging.</summary>
    public class UpdateLedgerJournalLinesHandler<TEntity> : IRequestHandler<UpdateLedgerJournalLinesCommand<TEntity>, Result> where TEntity : LedgerJournalLine
    {
        private readonly ILogger<UpdateLedgerJournalLinesHandler<TEntity>> _logger;
        private readonly IODataBatchService<TEntity> _service;

        public UpdateLedgerJournalLinesHandler(ILogger<UpdateLedgerJournalLinesHandler<TEntity>> logger, IODataBatchService<TEntity> service)
        {
            _logger = logger;
            _service = service;
        }

        public async Task<Result> Handle(UpdateLedgerJournalLinesCommand<TEntity> request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Updating Ledger Journal Lines in batch...");

            var result = await _service.UpdateBatchAsync(request.LedgerJournalLines, cancellationToken).ConfigureAwait(false);

            return result.Match(
                onSuccess: () =>
                {
                    _logger.LogInformation("Successfully updated {Count} Ledger Journal Lines.", request.LedgerJournalLines.Count);
                    return Result.Ok();
                },
                onFailure: error =>
                {
                    _logger.LogError("Failed to update Ledger Journal Lines. Error: {Error}", error.Message);
                    return Result.Fail(error);
                });
        }
    }
}
