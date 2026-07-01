using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;

/// <summary>Creates a single LedgerJournalHeader in D365 F&amp;O, emitting domain-specific structured logs. Retained over the generic CreateCommandHandler&lt;T&gt; because it adds journal-context logging.</summary>
public class CreateLedgerJournalHeaderHandler<TEntity> : IRequestHandler<CreateLedgerJournalHeaderCommand<TEntity>, Result<TEntity>> where TEntity : LedgerJournalHeader
{
    private readonly ILogger<CreateLedgerJournalHeaderHandler<TEntity>> _logger;
    private readonly IService<TEntity> _service;

    /// <summary>Initializes a new instance of the <see cref="CreateLedgerJournalHeaderHandler{TEntity}"/> class.</summary>
    public CreateLedgerJournalHeaderHandler(ILogger<CreateLedgerJournalHeaderHandler<TEntity>> logger, IService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    /// <inheritdoc/>
    public async Task<Result<TEntity>> Handle(CreateLedgerJournalHeaderCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating a new Ledger Journal Header in F&O with Journal Name: {JournalName} in Company: {Company}",
            request.LedgerJournalHeader.JournalName,
            request.LedgerJournalHeader.DataAreaId);

        var addResult = await _service.AddAsync(request.LedgerJournalHeader, cancellationToken).ConfigureAwait(false);

        return addResult.Match(
            onSuccess: entity =>
            {
                _logger.LogInformation("Successfully created Ledger Journal Header with Journal Name: {JournalName} and Journal Batch Number {JournalBatchNumber} in Company: {Company}",
                    request.LedgerJournalHeader.JournalName,
                    request.LedgerJournalHeader.JournalBatchNumber,
                    request.LedgerJournalHeader.DataAreaId);

                return Result.Ok(entity);
            },
            onFailure: error =>
            {
                return Result.Fail<TEntity>(error);
            });
    }
}
