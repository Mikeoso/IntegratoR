using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalHeader;

public class UpdateLedgerJournalHeaderHandler<TEntity> : IRequestHandler<UpdateLedgerJournalHeaderCommand<TEntity>, Result<TEntity>> where TEntity : LedgerJournalHeader
{
    private readonly ILogger<UpdateLedgerJournalHeaderCommand<TEntity>> _logger;
    private readonly IService<TEntity> _service;

    public UpdateLedgerJournalHeaderHandler(ILogger<UpdateLedgerJournalHeaderCommand<TEntity>> logger, IService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<Result<TEntity>> Handle(UpdateLedgerJournalHeaderCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating Ledger Journal Header in F&O with Journal Name: {JournalName}", request.LedgerJournalHeader.JournalName);

        var updateResult = await _service.UpdateAsync(request.LedgerJournalHeader, cancellationToken);

        if (updateResult.IsFailure)
        {
            return Result<TEntity>.Fail(updateResult);
        }

        _logger.LogInformation("Successfully updated Ledger Journal Header with ID: {JournalId}", updateResult.Value?.JournalName);

        return Result<TEntity>.Ok(updateResult.Value!);
    }
}
