using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalLine
{
    public record UpdateLedgerJournalLinesCommand<TEntity>(IEnumerable<TEntity> LedgerJournalLines) : ICommand<Result> where TEntity : LedgerJournalLine
    {
        public IReadOnlyDictionary<string, object> GetLoggingContext()
        {
            return new Dictionary<string, object>
            {
                { "Count", LedgerJournalLines.Count() },
                { "JournalBatchNumbers", string.Join(",", LedgerJournalLines.Select(l => l.JournalBatchNumber).Distinct()) }
            };
        }
    }
}
