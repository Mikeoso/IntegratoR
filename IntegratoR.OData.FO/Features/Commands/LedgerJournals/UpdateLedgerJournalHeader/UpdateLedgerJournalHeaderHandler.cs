using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalHeader;

/// <summary>Updates a single LedgerJournalHeader in D365 F&amp;O, emitting domain-specific structured logs. Retained over the generic UpdateCommandHandler&lt;T&gt; because it adds journal-context logging.</summary>
public class UpdateLedgerJournalHeaderHandler<TEntity> : IRequestHandler<UpdateLedgerJournalHeaderCommand<TEntity>, Result<TEntity>> where TEntity : LedgerJournalHeader
{
    private readonly ILogger<UpdateLedgerJournalHeaderHandler<TEntity>> _logger;
    private readonly IService<TEntity> _service;

    /// <summary>Initializes a new instance of the <see cref="UpdateLedgerJournalHeaderHandler{TEntity}"/> class.</summary>
    public UpdateLedgerJournalHeaderHandler(ILogger<UpdateLedgerJournalHeaderHandler<TEntity>> logger, IService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    /// <inheritdoc/>
    public async Task<Result<TEntity>> Handle(UpdateLedgerJournalHeaderCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating Ledger Journal Header in F&O with Journal Name: {JournalName}", request.LedgerJournalHeader.JournalName);

        var updateResult = await _service.UpdateAsync(request.LedgerJournalHeader, cancellationToken).ConfigureAwait(false);

        if (updateResult.IsFailed)
        {
            return Result.Fail<TEntity>(updateResult.Errors);
        }

        _logger.LogInformation("Successfully updated Ledger Journal Header with ID: {JournalId}", updateResult.Value?.JournalName);

        return Result.Ok(updateResult.Value!);
    }
}
