using FluentValidation;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalHeader;

/// <summary>Validates that <see cref="UpdateLedgerJournalHeadersCommand{TEntity}"/> carries a non-null, non-empty entity collection.</summary>
/// <typeparam name="TEntity">The <see cref="LedgerJournalHeader"/> type being updated.</typeparam>
public sealed class UpdateLedgerJournalHeadersCommandValidator<TEntity>
    : AbstractValidator<UpdateLedgerJournalHeadersCommand<TEntity>>
    where TEntity : LedgerJournalHeader
{
    /// <summary>Initializes a new instance of the <see cref="UpdateLedgerJournalHeadersCommandValidator{TEntity}"/> class.</summary>
    public UpdateLedgerJournalHeadersCommandValidator()
    {
        RuleFor(x => x.Entities)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Entities collection must not be null.")
            .Must(e => e.Any()).WithMessage("Entities collection must not be empty.");
    }
}
