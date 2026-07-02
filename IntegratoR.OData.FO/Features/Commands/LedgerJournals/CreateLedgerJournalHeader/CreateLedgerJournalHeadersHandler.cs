using FluentResults;
using IntegratoR.Abstractions.Common.Batch;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;

/// <summary>Creates a batch of LedgerJournalHeader entities in D365 F&amp;O, emitting domain-specific structured logs. Retained over the generic CreateBatchCommandHandler&lt;T&gt; because it adds journal-context (Count) logging.</summary>
/// <typeparam name="TEntity">The type of the entity being created.</typeparam>
public class CreateLedgerJournalHeadersHandler<TEntity> : IRequestHandler<CreateLedgerJournalHeadersCommand<TEntity>, Result<BatchOutcome>> where TEntity : LedgerJournalHeader
{
    private readonly ILogger<CreateLedgerJournalHeadersHandler<TEntity>> _logger;
    private readonly IODataBatchService<TEntity> _service;

    /// <summary>Initializes a new instance of the <see cref="CreateLedgerJournalHeadersHandler{TEntity}"/> class.</summary>
    public CreateLedgerJournalHeadersHandler(ILogger<CreateLedgerJournalHeadersHandler<TEntity>> logger, IODataBatchService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    /// <inheritdoc/>
    public async Task<Result<BatchOutcome>> Handle(CreateLedgerJournalHeadersCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating {Count} LedgerJournalHeader entities in F&O.", request.LedgerJournalHeaders.Count);

        Result<BatchOutcome> result = await _service.AddBatchAsync(request.LedgerJournalHeaders, request.Options, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            _logger.LogInformation("Successfully created {Count} LedgerJournalHeader entities in F&O.", request.LedgerJournalHeaders.Count);
        }
        return result;
    }
}
