using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;

public class CreateLedgerJournalHeaderHandler<TEntity>(ILogger<CreateLedgerJournalHeaderHandler<TEntity>> logger, IService<TEntity, string> service) : IRequestHandler<CreateLedgerJournalHeaderCommand<TEntity>, Result<TEntity>> where TEntity : LedgerJournalHeader
{
    private readonly ILogger<CreateLedgerJournalHeaderHandler<TEntity>> _logger = logger;
    private readonly IService<TEntity, string> _service = service;

    public async Task<Result<TEntity>> Handle(CreateLedgerJournalHeaderCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating a new Ledger Journal Header in F&O with Journal Name: {JournalName} in Company: {Company}",
            request.LedgerJournalHeader.JournalName,
            request.LedgerJournalHeader.DataAreaId);

        var addResult = await _service.AddAsync(request.LedgerJournalHeader, cancellationToken);

        if (addResult.IsFailure)
        {
            return Result<TEntity>.Fail(addResult);
        }

        _logger.LogInformation("Successfully created Ledger Journal Header with ID: {JournalId}", addResult.Value?.Id);

        return Result<TEntity>.Ok(addResult.Value!);
    }
}
