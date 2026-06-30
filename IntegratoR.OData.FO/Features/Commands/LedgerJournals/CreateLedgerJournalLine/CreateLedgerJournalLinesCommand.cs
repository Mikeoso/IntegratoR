using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;

public record CreateLedgerJournalLinesCommand<TEntity>(IReadOnlyList<TEntity> LedgerJournalLines)
    : CreateBatchCommand<TEntity>(LedgerJournalLines) where TEntity : LedgerJournalLine
{
    public override IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return new Dictionary<string, object>
        {
            { "Count", LedgerJournalLines.Count },
            { "JournalNames", string.Join(", ", LedgerJournalLines.Select(j => j.JournalBatchNumber)) }
        };
    }
}
