using FluentValidation;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Application.Features.Common.Validators;

/// <summary>
/// Validates the <see cref="CreateCommand{TEntity}"/> to ensure the entity is not null.
/// </summary>
/// <typeparam name="TEntity">The type of the entity to create.</typeparam>
public class CreateCommandValidator<TEntity> : AbstractValidator<CreateCommand<TEntity>>
    where TEntity : IEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateCommandValidator{TEntity}"/> class.
    /// </summary>
    public CreateCommandValidator()
    {
        RuleFor(x => x.Entity)
            .NotNull().WithMessage("Entity must not be null.");
    }
}
