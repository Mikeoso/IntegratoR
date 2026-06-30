using FluentValidation;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalHeader;

/// <summary>
/// Thin derived validator that re-applies the generic <c>UpdateBatchCommand</c> baseline rules
/// (entities not null, not empty) under the concrete batch command's closed type
/// (<c>IValidator&lt;UpdateLedgerJournalHeadersCommand&lt;TEntity&gt;&gt;</c>).
///
/// NOTE: currently dormant in the MediatR pipeline — FluentValidation's
/// <c>AddValidatorsFromAssembly</c> cannot register open-generic validators (see
/// <c>ServiceCollectionExtensions.cs</c> step 6). Unit-tested directly in
/// <c>GenericValidatorReuseTests</c>; will fire once a closed-generic validator-registration
/// mechanism is added.
/// </summary>
public sealed class UpdateLedgerJournalHeadersCommandValidator<TEntity>
    : AbstractValidator<UpdateLedgerJournalHeadersCommand<TEntity>>
    where TEntity : LedgerJournalHeader
{
    public UpdateLedgerJournalHeadersCommandValidator()
    {
        RuleFor(x => x.Entities)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Entities collection must not be null.")
            .Must(e => e.Any()).WithMessage("Entities collection must not be empty.");
    }
}
