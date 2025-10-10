using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalHeader;

public class UpdateLedgerJournalHandler<TEntity> : IRequestHandler<UpdateLedgerJournalHeadersCommand<TEntity>, Result> where TEntity : LedgerJournalHeader
{
    private readonly ILogger<UpdateLedgerJournalHeadersCommand<TEntity>> _logger;
    private readonly IODataBatchService<TEntity> _service;

    public UpdateLedgerJournalHandler(ILogger<UpdateLedgerJournalHeadersCommand<TEntity>> logger, IODataBatchService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<Result> Handle(UpdateLedgerJournalHeadersCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating Ledger Journal Headers in batch...");

        var result = await _service.UpdateBatchAsync(request.LedgerJournalHeaders, cancellationToken);

        return result.Match(
            onSuccess: () =>
            {
                _logger.LogInformation("Successfully updated {Count} Ledger Journal Headers.", request.LedgerJournalHeaders.Count());
                return Result.Ok();
            },
            onFailure: error =>
            {
                _logger.LogError("Failed to update Ledger Journal Headers. Error: {Error}", error.Message);
                return Result.Fail(error);
            });
    }
}
