using FluentValidation;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalLine;

/// <summary>Validates that <see cref="UpdateLedgerJournalLinesCommand{TEntity}"/> carries a non-null, non-empty entity collection.</summary>
/// <typeparam name="TEntity">The <see cref="LedgerJournalLine"/> type being updated.</typeparam>
public sealed class UpdateLedgerJournalLinesCommandValidator<TEntity>
    : AbstractValidator<UpdateLedgerJournalLinesCommand<TEntity>>
    where TEntity : LedgerJournalLine
{
    /// <summary>Initializes a new instance of the <see cref="UpdateLedgerJournalLinesCommandValidator{TEntity}"/> class.</summary>
    public UpdateLedgerJournalLinesCommandValidator()
    {
        RuleFor(x => x.Entities)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Entities collection must not be null.")
            .Must(e => e.Any()).WithMessage("Entities collection must not be empty.");
    }
}
