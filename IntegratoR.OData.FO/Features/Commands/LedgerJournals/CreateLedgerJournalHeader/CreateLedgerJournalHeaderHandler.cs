using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;

public class CreateLedgerJournalHeaderHandler<TEntity>(ILogger<CreateLedgerJournalHeaderHandler<TEntity>> logger, IService<TEntity> service) : IRequestHandler<CreateLedgerJournalHeaderCommand<TEntity>, Result<TEntity>> where TEntity : LedgerJournalHeader
{
    private readonly ILogger<CreateLedgerJournalHeaderHandler<TEntity>> _logger = logger;
    private readonly IService<TEntity> _service = service;

    public async Task<Result<TEntity>> Handle(CreateLedgerJournalHeaderCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating a new Ledger Journal Header in F&O with Journal Name: {JournalName} in Company: {Company}",
            request.LedgerJournalHeader.JournalName,
            request.LedgerJournalHeader.DataAreaId);

        var addResult = await _service.AddAsync(request.LedgerJournalHeader, cancellationToken);

        return addResult.Match(
            onSuccess: entity =>
            {
                _logger.LogInformation("Successfully created Ledger Journal Header with Journal Name: {JournalName} and Journal Batch Number {JournalBatchNumber} in Company: {Company}",
                    request.LedgerJournalHeader.JournalName,
                    request.LedgerJournalHeader.JournalBatchNumber,
                    request.LedgerJournalHeader.DataAreaId);

                return Result<TEntity>.Ok(entity);
            },
            onFailure: error =>
            {
                return Result<TEntity>.Fail(addResult);
            });
    }
}
