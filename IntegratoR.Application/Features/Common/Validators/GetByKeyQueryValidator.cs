using FluentValidation;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Application.Features.Common.Validators;

/// <summary>
/// Validates the <see cref="GetByKeyQuery{TEntity}"/> to ensure key values are provided.
/// </summary>
public class GetByKeyQueryValidator<TEntity> : AbstractValidator<GetByKeyQuery<TEntity>>
    where TEntity : class, IEntity
{
    public GetByKeyQueryValidator()
    {
        RuleFor(x => x.CompositeKey)
            .NotNull().WithMessage("Composite key must not be null.")
            .Must(k => k.Length > 0).WithMessage("Composite key must contain at least one value.")
            .ForEach(key => key
                .NotNull().WithMessage("Key values must not contain null elements."));
    }
}
