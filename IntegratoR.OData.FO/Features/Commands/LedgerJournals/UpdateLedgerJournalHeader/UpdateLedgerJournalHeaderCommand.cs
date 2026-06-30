using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalHeader;

public record UpdateLedgerJournalHeaderCommand<TEntity>(TEntity LedgerJournalHeader)
    : UpdateCommand<TEntity>(LedgerJournalHeader) where TEntity : LedgerJournalHeader;
