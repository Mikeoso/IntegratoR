using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalHeader;

public record UpdateLedgerJournalHeadersCommand<TEntity>(IEnumerable<TEntity> LedgerJournalHeaders) : ICommand<Result> where TEntity : LedgerJournalHeader;
