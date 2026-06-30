using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;

public record CreateLedgerJournalLineCommand<TEntity>(TEntity LedgerJournalLine)
    : CreateCommand<TEntity>(LedgerJournalLine) where TEntity : LedgerJournalLine;
