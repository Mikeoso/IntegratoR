using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;

/// <summary>
/// Represents a request to create a batch of ledger journal lines in D365 F&amp;O.
/// </summary>
/// <typeparam name="TEntity">The type of the ledger journal line entity.</typeparam>
public record CreateLedgerJournalLinesCommand<TEntity>(IReadOnlyList<TEntity> LedgerJournalLines)
    : CreateBatchCommand<TEntity>(LedgerJournalLines) where TEntity : LedgerJournalLine
{
    /// <summary>
    /// Gets the structured logging context for this command, including the line count and journal batch numbers.
    /// </summary>
    public override IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return new Dictionary<string, object>
        {
            { "Count", LedgerJournalLines.Count },
            { "JournalNames", string.Join(", ", LedgerJournalLines.Select(j => j.JournalBatchNumber)) }
        };
    }
}
