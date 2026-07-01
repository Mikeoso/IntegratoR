using FluentValidation;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalLine;

/// <summary>Validates that <see cref="UpdateLedgerJournalLineCommand{TEntity}"/> carries a non-null entity.</summary>
/// <typeparam name="TEntity">The <see cref="LedgerJournalLine"/> type being updated.</typeparam>
public sealed class UpdateLedgerJournalLineCommandValidator<TEntity>
    : AbstractValidator<UpdateLedgerJournalLineCommand<TEntity>>
    where TEntity : LedgerJournalLine
{
    /// <summary>Initializes a new instance of the <see cref="UpdateLedgerJournalLineCommandValidator{TEntity}"/> class.</summary>
    public UpdateLedgerJournalLineCommandValidator()
    {
        RuleFor(x => x.Entity)
            .NotNull().WithMessage("Entity must not be null.");
    }
}
