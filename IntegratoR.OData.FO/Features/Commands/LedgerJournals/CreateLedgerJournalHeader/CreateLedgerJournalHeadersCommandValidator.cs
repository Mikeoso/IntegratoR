using FluentValidation;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;

/// <summary>Validates that <see cref="CreateLedgerJournalHeadersCommand{TEntity}"/> carries a non-null, non-empty entity collection.</summary>
/// <typeparam name="TEntity">The <see cref="LedgerJournalHeader"/> type being created.</typeparam>
public sealed class CreateLedgerJournalHeadersCommandValidator<TEntity>
    : AbstractValidator<CreateLedgerJournalHeadersCommand<TEntity>>
    where TEntity : LedgerJournalHeader
{
    /// <summary>Initializes a new instance of the <see cref="CreateLedgerJournalHeadersCommandValidator{TEntity}"/> class.</summary>
    public CreateLedgerJournalHeadersCommandValidator()
    {
        RuleFor(x => x.Entities)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Entities collection must not be null.")
            .Must(e => e.Any()).WithMessage("Entities collection must not be empty.");
    }
}
