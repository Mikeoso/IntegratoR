using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;

/// <summary>Creates a single LedgerJournalLine in D365 F&amp;O, emitting domain-specific structured logs. Retained over the generic CreateCommandHandler&lt;T&gt; because it adds journal-context logging.</summary>
public class CreateLedgerJournalLineHandler<TEntity> : IRequestHandler<CreateLedgerJournalLineCommand<TEntity>, Result<TEntity>> where TEntity : LedgerJournalLine
{
    private readonly ILogger<CreateLedgerJournalLineHandler<TEntity>> _logger;
    private readonly IService<TEntity> _service;

    /// <summary>Initializes a new instance of the <see cref="CreateLedgerJournalLineHandler{TEntity}"/> class.</summary>
    public CreateLedgerJournalLineHandler(ILogger<CreateLedgerJournalLineHandler<TEntity>> logger, IService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    /// <inheritdoc/>
    public async Task<Result<TEntity>> Handle(CreateLedgerJournalLineCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating a new Ledger Journal Line for Header: {JournalBatchNumber} in Company: {Company}",
            request.LedgerJournalLine.JournalBatchNumber,
            request.LedgerJournalLine.DataAreaId);

        var addResult = await _service.AddAsync(request.LedgerJournalLine, cancellationToken).ConfigureAwait(false);

        return addResult.Match(
            onSuccess: entity =>
            {
                _logger.LogInformation(
                    "Successfully created Ledger Journal Line with Line Number: {LineNumber} for Header: {JournalBatchNumber} in Company: {Company}",
                    entity.LineNumber,
                    entity.JournalBatchNumber,
                    entity.DataAreaId);

                return Result.Ok(entity);
            },
            onFailure: error =>
            {
                _logger.LogError(
                    "Failed to create Ledger Journal Line for Header: {JournalBatchNumber} in Company: {Company}. Error: {Error}",
                    request.LedgerJournalLine.JournalBatchNumber,
                    request.LedgerJournalLine.DataAreaId,
                    error.Message);

                return Result.Fail<TEntity>(error);
            });
    }
}
