using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalHeader;

/// <summary>
/// Represents a request to update a batch of ledger journal headers in D365 F&amp;O.
/// </summary>
/// <typeparam name="TEntity">The type of the ledger journal header entity.</typeparam>
public record UpdateLedgerJournalHeadersCommand<TEntity>(IReadOnlyList<TEntity> LedgerJournalHeaders) : UpdateBatchCommand<TEntity>(LedgerJournalHeaders) where TEntity : LedgerJournalHeader
{
    /// <summary>
    /// Gets the structured logging context for this command, including the header count and journal names.
    /// </summary>
    public override IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return new Dictionary<string, object>
        {
            { "Count", LedgerJournalHeaders.Count },
            { "JournalNames", string.Join(", ", LedgerJournalHeaders.Select(j => j.JournalName)) }
        };
    }
}
