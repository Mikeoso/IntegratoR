using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;

public class CreateLedgerJournalLinesHandler<TEntity> : IRequestHandler<CreateLedgerJournalLinesCommand<TEntity>, Result> where TEntity : LedgerJournalLine
{
    private readonly ILogger<CreateLedgerJournalLinesHandler<TEntity>> _logger;
    private readonly IODataBatchService<TEntity, string> _service;

    public CreateLedgerJournalLinesHandler(ILogger<CreateLedgerJournalLinesHandler<TEntity>> logger, IODataBatchService<TEntity, string> service)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<Result> Handle(CreateLedgerJournalLinesCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating {Count} Ledger Journal Lines in F&O.", request.LedgerJournalLines.Count());

        var addResult = await _service.AddBatchAsync(request.LedgerJournalLines, cancellationToken);

        if (addResult.IsFailure)
        {
            _logger.LogError("Failed to create Ledger Journal Lines: {Error}", addResult.Error);
            return Result.Fail(addResult.Error!);
        }

        _logger.LogInformation("Successfully created {Count} Ledger Journal Lines in F&O.", request.LedgerJournalLines.Count());

        return Result.Ok();
    }
}
