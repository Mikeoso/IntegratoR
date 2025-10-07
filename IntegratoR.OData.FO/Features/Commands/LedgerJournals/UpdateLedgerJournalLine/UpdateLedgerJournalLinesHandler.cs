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
        private IODataBatchService<TEntity, string> _batchService;

        public UpdateLedgerJournalLinesHandler(ILogger<UpdateLedgerJournalLinesHandler<TEntity>> logger, IODataBatchService<TEntity, string> batchService)
        {
            _logger = logger;
            _batchService = batchService;
        }

        public async Task<Result> Handle(UpdateLedgerJournalLinesCommand<TEntity> request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Updating Ledger Journal Lines in batch...");

            var result = await _batchService.UpdateBatchAsync(request.LedgerJournalLines, cancellationToken);

            if (result.IsFailure)
            {
                return result;
            }
            _logger.LogInformation("Successfully updated {Count} Ledger Journal Lines.", request.LedgerJournalLines.Count());
            return Result.Ok();
        }
    }
}
