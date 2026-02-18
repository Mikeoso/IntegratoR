using FluentValidation;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Application.Features.Common.Validators;

/// <summary>
/// Validates the <see cref="UpdateCommand{TEntity}"/> to ensure the entity is not null.
/// </summary>
public class UpdateCommandValidator<TEntity> : AbstractValidator<UpdateCommand<TEntity>>
    where TEntity : IEntity
{
    public UpdateCommandValidator()
    {
        RuleFor(x => x.Entity)
            .NotNull().WithMessage("Entity must not be null.");
    }
}
