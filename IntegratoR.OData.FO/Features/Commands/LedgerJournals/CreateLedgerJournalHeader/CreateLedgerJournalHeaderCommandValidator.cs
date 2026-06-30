using FluentValidation;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;

/// <summary>
/// Thin derived validator that re-applies the generic <c>CreateCommand</c> baseline rule
/// (entity not null) under the concrete command's closed type
/// (<c>IValidator&lt;CreateLedgerJournalHeaderCommand&lt;TEntity&gt;&gt;</c>).
///
/// NOTE: currently dormant in the MediatR pipeline — FluentValidation's
/// <c>AddValidatorsFromAssembly</c> cannot register open-generic validators (see
/// <c>ServiceCollectionExtensions.cs</c> step 6). Unit-tested directly in
/// <c>GenericValidatorReuseTests</c>; will fire once a closed-generic validator-registration
/// mechanism is added.
/// </summary>
public sealed class CreateLedgerJournalHeaderCommandValidator<TEntity>
    : AbstractValidator<CreateLedgerJournalHeaderCommand<TEntity>>
    where TEntity : LedgerJournalHeader
{
    public CreateLedgerJournalHeaderCommandValidator()
    {
        RuleFor(x => x.Entity)
            .NotNull().WithMessage("Entity must not be null.");
    }
}
