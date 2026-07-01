using FluentValidation;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;

/// <summary>Validates that <see cref="CreateLedgerJournalHeaderCommand{TEntity}"/> carries a non-null entity.</summary>
/// <typeparam name="TEntity">The <see cref="LedgerJournalHeader"/> type being created.</typeparam>
public sealed class CreateLedgerJournalHeaderCommandValidator<TEntity>
    : AbstractValidator<CreateLedgerJournalHeaderCommand<TEntity>>
    where TEntity : LedgerJournalHeader
{
    /// <summary>Initializes a new instance of the <see cref="CreateLedgerJournalHeaderCommandValidator{TEntity}"/> class.</summary>
    public CreateLedgerJournalHeaderCommandValidator()
    {
        RuleFor(x => x.Entity)
            .NotNull().WithMessage("Entity must not be null.");
    }
}
