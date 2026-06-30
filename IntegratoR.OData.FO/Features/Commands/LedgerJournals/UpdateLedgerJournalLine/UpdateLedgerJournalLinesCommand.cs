using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalLine
{
    public record UpdateLedgerJournalLinesCommand<TEntity>(IReadOnlyList<TEntity> LedgerJournalLines) : UpdateBatchCommand<TEntity>(LedgerJournalLines) where TEntity : LedgerJournalLine
    {
        public override IReadOnlyDictionary<string, object> GetLoggingContext()
        {
            return new Dictionary<string, object>
            {
                { "Count", LedgerJournalLines.Count },
                { "JournalBatchNumbers", string.Join(",", LedgerJournalLines.Select(l => l.JournalBatchNumber).Distinct()) }
            };
        }
    }
}
