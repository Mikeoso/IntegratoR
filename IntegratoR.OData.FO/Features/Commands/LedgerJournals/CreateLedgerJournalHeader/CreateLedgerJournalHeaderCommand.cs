using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;

public record CreateLedgerJournalHeaderCommand<TEntity>(TEntity LedgerJournalHeader)
    : CreateCommand<TEntity>(LedgerJournalHeader) where TEntity : LedgerJournalHeader;