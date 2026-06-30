using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalHeader;

public record UpdateLedgerJournalHeadersCommand<TEntity>(IReadOnlyList<TEntity> LedgerJournalHeaders) : UpdateBatchCommand<TEntity>(LedgerJournalHeaders) where TEntity : LedgerJournalHeader
{
    public override IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return new Dictionary<string, object>
        {
            { "Count", LedgerJournalHeaders.Count },
            { "JournalNames", string.Join(", ", LedgerJournalHeaders.Select(j => j.JournalName)) }
        };
    }
}
