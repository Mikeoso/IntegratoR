using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;

/// <summary>
/// Represents a request to create a ledger journal header in D365 F&amp;O.
/// </summary>
/// <typeparam name="TEntity">The type of the ledger journal header entity.</typeparam>
public record CreateLedgerJournalHeaderCommand<TEntity>(TEntity LedgerJournalHeader)
    : CreateCommand<TEntity>(LedgerJournalHeader) where TEntity : LedgerJournalHeader;
