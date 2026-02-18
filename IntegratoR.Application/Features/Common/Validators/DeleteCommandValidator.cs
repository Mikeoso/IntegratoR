using FluentValidation;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Application.Features.Common.Validators;

/// <summary>
/// Validates the <see cref="DeleteCommand{TEntity}"/> to ensure the entity is not null.
/// </summary>
public class DeleteCommandValidator<TEntity> : AbstractValidator<DeleteCommand<TEntity>>
    where TEntity : IEntity
{
    public DeleteCommandValidator()
    {
        RuleFor(x => x.Entity)
            .NotNull().WithMessage("Entity must not be null.");
    }
}
