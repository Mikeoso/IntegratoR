using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;

public record CreateLedgerJournalHeadersCommand<TEntity>(IEnumerable<TEntity> LedgerJournalHeaders) : CreateBatchCommand<TEntity>(LedgerJournalHeaders) where TEntity : LedgerJournalHeader
{
    public override IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return new Dictionary<string, object>
        {
            { "EntityType", typeof(TEntity).Name  },
            { "Count", LedgerJournalHeaders.Count() },
            { "JournalNames", string.Join(", ", LedgerJournalHeaders.Select(j => j.JournalName)) }
        };
    }
}