using FluentValidation;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

namespace IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;

/// <summary>
/// Thin derived validator that re-applies the generic <c>CreateCommand</c> baseline rule
/// (entity not null) under the concrete command's closed type
/// (<c>IValidator&lt;CreateLedgerJournalLineCommand&lt;TEntity&gt;&gt;</c>).
/// </summary>
/// <remarks>
/// The container resolves <c>IValidator&lt;ConcreteCommand&gt;</c> by exact closed type, so inheriting
/// the generic base command is not enough — the validator must close over the concrete command type.
///
/// NOTE: currently dormant in the MediatR pipeline — FluentValidation's
/// <c>AddValidatorsFromAssembly</c> cannot register open-generic validators (see
/// <c>ServiceCollectionExtensions.cs</c> step 6). Unit-tested directly in
/// <c>GenericValidatorReuseTests</c>; will fire once a closed-generic validator-registration
/// mechanism is added.
/// </remarks>
public sealed class CreateLedgerJournalLineCommandValidator<TEntity>
    : AbstractValidator<CreateLedgerJournalLineCommand<TEntity>>
    where TEntity : LedgerJournalLine
{
    public CreateLedgerJournalLineCommandValidator()
    {
        RuleFor(x => x.Entity)
            .NotNull().WithMessage("Entity must not be null.");
    }
}
