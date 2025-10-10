using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using System.Reflection.Metadata.Ecma335;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;

public record CreateLedgerJournalLineCommand<TEntity>(TEntity LedgerJournalLine) : ICommand<Result<TEntity>> where TEntity : LedgerJournalLine
{
    public IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return LedgerJournalLine.GetLoggingContext();
    }
}