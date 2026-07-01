using FluentValidation;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Application.Features.Common.Validators;

/// <summary>
/// Validates the <see cref="UpdateBatchCommand{TEntity}"/> to ensure the entities collection is valid.
/// </summary>
/// <typeparam name="TEntity">The type of the entities to update.</typeparam>
public class UpdateBatchCommandValidator<TEntity> : AbstractValidator<UpdateBatchCommand<TEntity>>
    where TEntity : IEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateBatchCommandValidator{TEntity}"/> class.
    /// </summary>
    public UpdateBatchCommandValidator()
    {
        RuleFor(x => x.Entities)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Entities collection must not be null.")
            .Must(e => e.Any()).WithMessage("Entities collection must not be empty.");
    }
}
