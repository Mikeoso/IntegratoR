using FluentValidation;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Application.Features.Common.Validators;

/// <summary>
/// Validates the <see cref="DeleteBatchCommand{TEntity}"/> to ensure the entities collection is valid.
/// </summary>
/// <typeparam name="TEntity">The type of the entities to delete.</typeparam>
public class DeleteBatchCommandValidator<TEntity> : AbstractValidator<DeleteBatchCommand<TEntity>>
    where TEntity : IEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteBatchCommandValidator{TEntity}"/> class.
    /// </summary>
    public DeleteBatchCommandValidator()
    {
        RuleFor(x => x.Entities)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Entities collection must not be null.")
            .Must(e => e.Any()).WithMessage("Entities collection must not be empty.");
    }
}
