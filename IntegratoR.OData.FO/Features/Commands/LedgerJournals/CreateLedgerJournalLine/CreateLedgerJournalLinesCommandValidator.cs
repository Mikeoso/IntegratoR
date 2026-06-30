using FluentValidation;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;

/// <summary>
/// Thin derived validator that re-applies the generic <c>CreateBatchCommand</c> baseline rules
/// (entities not null, not empty) under the concrete batch command's closed type
/// (<c>IValidator&lt;CreateLedgerJournalLinesCommand&lt;TEntity&gt;&gt;</c>).
///
/// NOTE: currently dormant in the MediatR pipeline — FluentValidation's
/// <c>AddValidatorsFromAssembly</c> cannot register open-generic validators (see
/// <c>ServiceCollectionExtensions.cs</c> step 6). Unit-tested directly in
/// <c>GenericValidatorReuseTests</c>; will fire once a closed-generic validator-registration
/// mechanism is added.
/// </summary>
public sealed class CreateLedgerJournalLinesCommandValidator<TEntity>
    : AbstractValidator<CreateLedgerJournalLinesCommand<TEntity>>
    where TEntity : LedgerJournalLine
{
    public CreateLedgerJournalLinesCommandValidator()
    {
        RuleFor(x => x.Entities)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Entities collection must not be null.")
            .Must(e => e.Any()).WithMessage("Entities collection must not be empty.");
    }
}
