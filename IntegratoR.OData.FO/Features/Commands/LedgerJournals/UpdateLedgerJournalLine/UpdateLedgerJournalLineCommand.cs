using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalLine;

public record UpdateLedgerJournalLineCommand<TEntity>(TEntity LedgerJournalLine) : ICommand<Result<TEntity>> where TEntity : LedgerJournalLine;
