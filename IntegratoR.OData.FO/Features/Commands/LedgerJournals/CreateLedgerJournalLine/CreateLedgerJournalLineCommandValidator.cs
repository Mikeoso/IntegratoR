using FluentValidation;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;

/// <summary>Validates that <see cref="CreateLedgerJournalLineCommand{TEntity}"/> carries a non-null entity.</summary>
/// <typeparam name="TEntity">The <see cref="LedgerJournalLine"/> type being created.</typeparam>
public sealed class CreateLedgerJournalLineCommandValidator<TEntity>
    : AbstractValidator<CreateLedgerJournalLineCommand<TEntity>>
    where TEntity : LedgerJournalLine
{
    /// <summary>Initializes a new instance of the <see cref="CreateLedgerJournalLineCommandValidator{TEntity}"/> class.</summary>
    public CreateLedgerJournalLineCommandValidator()
    {
        RuleFor(x => x.Entity)
            .NotNull().WithMessage("Entity must not be null.");
    }
}
