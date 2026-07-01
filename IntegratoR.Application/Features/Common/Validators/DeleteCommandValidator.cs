using FluentValidation;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Application.Features.Common.Validators;

/// <summary>
/// Validates the <see cref="DeleteCommand{TEntity}"/> to ensure the entity is not null.
/// </summary>
/// <typeparam name="TEntity">The type of the entity to delete.</typeparam>
public class DeleteCommandValidator<TEntity> : AbstractValidator<DeleteCommand<TEntity>>
    where TEntity : IEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteCommandValidator{TEntity}"/> class.
    /// </summary>
    public DeleteCommandValidator()
    {
        RuleFor(x => x.Entity)
            .NotNull().WithMessage("Entity must not be null.");
    }
}
