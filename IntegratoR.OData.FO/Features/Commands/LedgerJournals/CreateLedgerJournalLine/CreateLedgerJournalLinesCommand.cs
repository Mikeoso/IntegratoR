using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;

public record CreateLedgerJournalLinesCommand<TEntity>(IEnumerable<TEntity> LedgerJournalLines) : ICommand<Result> where TEntity : LedgerJournalLine
{
    public IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return new Dictionary<string, object>
        {
            { "Count", LedgerJournalLines.Count() },
            { "JournalNames", string.Join(", ", LedgerJournalLines.Select(j => j.JournalBatchNumber)) }
        };
    }
}