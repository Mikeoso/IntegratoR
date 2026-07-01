using FluentValidation;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalHeader;

/// <summary>Validates that <see cref="UpdateLedgerJournalHeaderCommand{TEntity}"/> carries a non-null entity.</summary>
/// <typeparam name="TEntity">The <see cref="LedgerJournalHeader"/> type being updated.</typeparam>
public sealed class UpdateLedgerJournalHeaderCommandValidator<TEntity>
    : AbstractValidator<UpdateLedgerJournalHeaderCommand<TEntity>>
    where TEntity : LedgerJournalHeader
{
    /// <summary>Initializes a new instance of the <see cref="UpdateLedgerJournalHeaderCommandValidator{TEntity}"/> class.</summary>
    public UpdateLedgerJournalHeaderCommandValidator()
    {
        RuleFor(x => x.Entity)
            .NotNull().WithMessage("Entity must not be null.");
    }
}
