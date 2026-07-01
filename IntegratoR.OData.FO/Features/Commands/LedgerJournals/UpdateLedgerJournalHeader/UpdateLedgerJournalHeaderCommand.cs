using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalHeader;

/// <summary>
/// Represents a request to update a ledger journal header in D365 F&amp;O.
/// </summary>
/// <typeparam name="TEntity">The type of the ledger journal header entity.</typeparam>
public record UpdateLedgerJournalHeaderCommand<TEntity>(TEntity LedgerJournalHeader)
    : UpdateCommand<TEntity>(LedgerJournalHeader) where TEntity : LedgerJournalHeader;
