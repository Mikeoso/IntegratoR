using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalLine;

/// <summary>Updates a single LedgerJournalLine in D365 F&amp;O, emitting domain-specific structured logs. Retained over the generic UpdateCommandHandler&lt;T&gt; because it adds journal-context logging.</summary>
/// <typeparam name="TEntity">The type of the entity being updated.</typeparam>
public class UpdateLedgerJournalLineHandler<TEntity> : IRequestHandler<UpdateLedgerJournalLineCommand<TEntity>, Result<TEntity>> where TEntity : LedgerJournalLine
{
    private readonly ILogger<UpdateLedgerJournalLineHandler<TEntity>> _logger;
    private readonly IService<TEntity> _service;

    /// <summary>Initializes a new instance of the <see cref="UpdateLedgerJournalLineHandler{TEntity}"/> class.</summary>
    public UpdateLedgerJournalLineHandler(ILogger<UpdateLedgerJournalLineHandler<TEntity>> logger, IService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    /// <inheritdoc/>
    public async Task<Result<TEntity>> Handle(UpdateLedgerJournalLineCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating Ledger Journal Line with Journal Number: {JournalBatchNumber} and Line Number: {LineNumber} in Company {Company}",
            request.LedgerJournalLine.JournalBatchNumber,
            request.LedgerJournalLine.LineNumber,
            request.LedgerJournalLine.DataAreaId);

        var updateResult = await _service.UpdateAsync(request.LedgerJournalLine, cancellationToken).ConfigureAwait(false);

        return updateResult.Match(
            onSuccess: updatedEntity =>
            {
                _logger.LogInformation("Successfully updated Ledger Journal Line with Journal Number: {JournalBatchNumber} and Line Number: {LineNumber} in Company {Company}",
                    request.LedgerJournalLine.JournalBatchNumber,
                    request.LedgerJournalLine.LineNumber,
                    request.LedgerJournalLine.DataAreaId);
                return Result.Ok(updatedEntity);
            },
            onFailure: error =>
            {
                _logger.LogError("Failed to update Ledger Journal Line with Journal Number: {JournalBatchNumber} and Line Number: {LineNumber} in Company {Company}. Error: {Error}",
                    request.LedgerJournalLine.JournalBatchNumber,
                    request.LedgerJournalLine.LineNumber,
                    request.LedgerJournalLine.DataAreaId,
                    error.Message);
                return Result.Fail<TEntity>(error);
            });
    }
}
