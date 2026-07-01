using FluentValidation;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Application.Features.Common.Validators;

/// <summary>
/// Validates the <see cref="UpdateCommand{TEntity}"/> to ensure the entity is not null.
/// </summary>
/// <typeparam name="TEntity">The type of the entity to update.</typeparam>
public class UpdateCommandValidator<TEntity> : AbstractValidator<UpdateCommand<TEntity>>
    where TEntity : IEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateCommandValidator{TEntity}"/> class.
    /// </summary>
    public UpdateCommandValidator()
    {
        RuleFor(x => x.Entity)
            .NotNull().WithMessage("Entity must not be null.");
    }
}
