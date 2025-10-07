using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;

public record CreateLedgerJournalHeaderCommand<TEntity>(TEntity LedgerJournalHeader) : ICommand<Result<TEntity>> where TEntity : LedgerJournalHeader;
