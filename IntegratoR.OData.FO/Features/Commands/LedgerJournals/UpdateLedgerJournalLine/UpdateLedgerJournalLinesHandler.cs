using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalLine
{
    public class UpdateLedgerJournalLinesHandler<TEntity> : IRequestHandler<UpdateLedgerJournalLinesCommand<TEntity>, Result> where TEntity : LedgerJournalLine
    {
        private ILogger<UpdateLedgerJournalLinesHandler<TEntity>> _logger;
        private IODataBatchService<TEntity> _batchService;

        public UpdateLedgerJournalLinesHandler(ILogger<UpdateLedgerJournalLinesHandler<TEntity>> logger, IODataBatchService<TEntity> batchService)
        {
            _logger = logger;
            _batchService = batchService;
        }

        public async Task<Result> Handle(UpdateLedgerJournalLinesCommand<TEntity> request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Updating Ledger Journal Lines in batch...");

            var result = await _batchService.UpdateBatchAsync(request.LedgerJournalLines, cancellationToken);

            return result.Match(
                onSuccess: () =>
                {
                    _logger.LogInformation("Successfully updated {Count} Ledger Journal Lines.", request.LedgerJournalLines.Count());
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
