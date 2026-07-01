using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalLine;

/// <summary>
/// Represents a request to update a ledger journal line in D365 F&amp;O.
/// </summary>
/// <typeparam name="TEntity">The type of the ledger journal line entity.</typeparam>
public record UpdateLedgerJournalLineCommand<TEntity>(TEntity LedgerJournalLine)
    : UpdateCommand<TEntity>(LedgerJournalLine) where TEntity : LedgerJournalLine;
