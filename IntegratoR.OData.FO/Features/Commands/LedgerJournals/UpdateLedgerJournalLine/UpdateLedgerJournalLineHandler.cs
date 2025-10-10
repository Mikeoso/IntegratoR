using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalLine;

public class UpdateLedgerJournalLineHandler<TEntity> : IRequestHandler<UpdateLedgerJournalLineCommand<TEntity>, Result<TEntity>> where TEntity : LedgerJournalLine
{
    private readonly ILogger<UpdateLedgerJournalLineHandler<TEntity>> _logger;
    private readonly IService<TEntity> _service;

    public UpdateLedgerJournalLineHandler(ILogger<UpdateLedgerJournalLineHandler<TEntity>> logger, IService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<Result<TEntity>> Handle(UpdateLedgerJournalLineCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating Ledger Journal Line with Journal Number: {JournalBatchNumber} and Line Number: {LineNumber} in Company {Company}",
            request.LedgerJournalLine.JournalBatchNumber,
            request.LedgerJournalLine.LineNumber,
            request.LedgerJournalLine.DataAreaId);

        var updateResult = await _service.UpdateAsync(request.LedgerJournalLine, cancellationToken);

        return updateResult.Match(
            onSuccess: updatedEntity =>
            {
                _logger.LogInformation("Successfully updated Ledger Journal Line with Journal Number: {JournalBatchNumber} and Line Number: {LineNumber} in Company {Company}",
                    request.LedgerJournalLine.JournalBatchNumber,
                    request.LedgerJournalLine.LineNumber,
                    request.LedgerJournalLine.DataAreaId);
                return Result<TEntity>.Ok(updatedEntity);
            },
            onFailure: error =>
            {
                _logger.LogError("Failed to update Ledger Journal Line with Journal Number: {JournalBatchNumber} and Line Number: {LineNumber} in Company {Company}. Error: {Error}",
                    request.LedgerJournalLine.JournalBatchNumber,
                    request.LedgerJournalLine.LineNumber,
                    request.LedgerJournalLine.DataAreaId,
                    error.Message);
                return Result<TEntity>.Fail(error);
            });
    }
}
