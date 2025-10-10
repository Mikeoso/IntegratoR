using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;

public class CreateLedgerJournalHeadersHandler<TEntity> : IRequestHandler<CreateLedgerJournalHeadersCommand<TEntity>, Result> where TEntity : LedgerJournalHeader
{
    private readonly ILogger<CreateLedgerJournalHeadersHandler<TEntity>> _logger;
    private readonly IODataBatchService<TEntity> _service;

    public CreateLedgerJournalHeadersHandler(ILogger<CreateLedgerJournalHeadersHandler<TEntity>> logger, IODataBatchService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<Result> Handle(CreateLedgerJournalHeadersCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating {Count} LedgerJournalHeader entities in F&O.", request.LedgerJournalHeaders.Count());

        var addResult = await _service.AddBatchAsync(request.LedgerJournalHeaders, cancellationToken);

        return addResult.Match(
            onSuccess: () =>
            {
                _logger.LogInformation("Successfully created {Count} LedgerJournalHeader entities in F&O.", request.LedgerJournalHeaders.Count());

                return Result.Ok();
            },
            onFailure: error =>
            {
                return Result.Fail(error);
            });
    }
}
