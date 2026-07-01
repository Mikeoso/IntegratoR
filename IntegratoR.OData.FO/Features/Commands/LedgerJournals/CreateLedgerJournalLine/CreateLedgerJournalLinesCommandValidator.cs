using FluentValidation;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;

/// <summary>Validates that <see cref="CreateLedgerJournalLinesCommand{TEntity}"/> carries a non-null, non-empty entity collection.</summary>
/// <typeparam name="TEntity">The <see cref="LedgerJournalLine"/> type being created.</typeparam>
public sealed class CreateLedgerJournalLinesCommandValidator<TEntity>
    : AbstractValidator<CreateLedgerJournalLinesCommand<TEntity>>
    where TEntity : LedgerJournalLine
{
    /// <summary>Initializes a new instance of the <see cref="CreateLedgerJournalLinesCommandValidator{TEntity}"/> class.</summary>
    public CreateLedgerJournalLinesCommandValidator()
    {
        RuleFor(x => x.Entities)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Entities collection must not be null.")
            .Must(e => e.Any()).WithMessage("Entities collection must not be empty.");
    }
}
