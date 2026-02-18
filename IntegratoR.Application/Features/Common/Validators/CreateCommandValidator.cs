using FluentValidation;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Application.Features.Common.Validators;

/// <summary>
/// Validates the <see cref="CreateCommand{TEntity}"/> to ensure the entity is not null.
/// </summary>
public class CreateCommandValidator<TEntity> : AbstractValidator<CreateCommand<TEntity>>
    where TEntity : IEntity
{
    public CreateCommandValidator()
    {
        RuleFor(x => x.Entity)
            .NotNull().WithMessage("Entity must not be null.");
    }
}
