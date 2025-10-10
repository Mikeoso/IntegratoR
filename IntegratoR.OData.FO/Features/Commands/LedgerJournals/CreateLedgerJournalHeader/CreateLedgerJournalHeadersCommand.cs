using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;

public record CreateLedgerJournalHeadersCommand<TEntity>(IEnumerable<TEntity> LedgerJournalHeaders) : ICommand<Result> where TEntity : LedgerJournalHeader
{
    public IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return new Dictionary<string, object>
        {
            { "Count", LedgerJournalHeaders.Count() },
            { "JournalNames", string.Join(", ", LedgerJournalHeaders.Select(j => j.JournalName)) }
        };
    }
}