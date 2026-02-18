using FluentValidation;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Application.Features.Common.Validators;

/// <summary>
/// Validates the <see cref="UpdateBatchCommand{TEntity}"/> to ensure the entities collection is valid.
/// </summary>
public class UpdateBatchCommandValidator<TEntity> : AbstractValidator<UpdateBatchCommand<TEntity>>
    where TEntity : IEntity
{
    public UpdateBatchCommandValidator()
    {
        RuleFor(x => x.Entities)
            .NotNull().WithMessage("Entities collection must not be null.")
            .Must(e => e.Any()).WithMessage("Entities collection must not be empty.");
    }
}
