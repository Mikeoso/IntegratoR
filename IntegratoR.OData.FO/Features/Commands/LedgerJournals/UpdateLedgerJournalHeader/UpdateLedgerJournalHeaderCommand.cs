using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalHeader;

public record UpdateLedgerJournalHeaderCommand<TEntity>(TEntity LedgerJournalHeader) : ICommand<Result<TEntity>> where TEntity : LedgerJournalHeader
{
    public IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return LedgerJournalHeader.GetLoggingContext();
    }
}