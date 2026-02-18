using FluentValidation;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Application.Features.Common.Validators;

/// <summary>
/// Validates the <see cref="CreateBatchCommand{TEntity}"/> to ensure the entities collection is valid.
/// </summary>
public class CreateBatchCommandValidator<TEntity> : AbstractValidator<CreateBatchCommand<TEntity>>
    where TEntity : IEntity
{
    public CreateBatchCommandValidator()
    {
        RuleFor(x => x.Entities)
            .NotNull().WithMessage("Entities collection must not be null.")
            .Must(e => e.Any()).WithMessage("Entities collection must not be empty.");
    }
}
