using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;

/// <summary>
/// Represents a request to create a batch of ledger journal headers in D365 F&amp;O.
/// </summary>
/// <typeparam name="TEntity">The type of the ledger journal header entity.</typeparam>
public record CreateLedgerJournalHeadersCommand<TEntity>(IReadOnlyList<TEntity> LedgerJournalHeaders) : CreateBatchCommand<TEntity>(LedgerJournalHeaders) where TEntity : LedgerJournalHeader
{
    /// <summary>
    /// Gets the structured logging context for this command, including the entity type, header count, and journal names.
    /// </summary>
    public override IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return new Dictionary<string, object>
        {
            { "EntityType", typeof(TEntity).Name  },
            { "Count", LedgerJournalHeaders.Count },
            { "JournalNames", string.Join(", ", LedgerJournalHeaders.Select(j => j.JournalName)) }
        };
    }
}
