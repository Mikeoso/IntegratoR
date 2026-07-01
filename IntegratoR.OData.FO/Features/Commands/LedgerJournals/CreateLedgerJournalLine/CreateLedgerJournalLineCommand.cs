using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;

/// <summary>
/// Represents a request to create a ledger journal line in D365 F&amp;O.
/// </summary>
/// <typeparam name="TEntity">The type of the ledger journal line entity.</typeparam>
public record CreateLedgerJournalLineCommand<TEntity>(TEntity LedgerJournalLine)
    : CreateCommand<TEntity>(LedgerJournalLine) where TEntity : LedgerJournalLine;
