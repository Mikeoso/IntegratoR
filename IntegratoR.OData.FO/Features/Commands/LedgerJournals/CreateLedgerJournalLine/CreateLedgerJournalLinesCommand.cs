using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;

public record CreateLedgerJournalLinesCommand<TEntity>(IEnumerable<TEntity> LedgerJournalLines) : ICommand<Result> where TEntity : LedgerJournalLine;
