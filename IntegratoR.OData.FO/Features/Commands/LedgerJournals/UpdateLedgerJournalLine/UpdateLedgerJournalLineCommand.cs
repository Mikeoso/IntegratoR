using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalLine;

public record UpdateLedgerJournalLineCommand<TEntity>(TEntity LedgerJournalLine)
    : UpdateCommand<TEntity>(LedgerJournalLine) where TEntity : LedgerJournalLine;
