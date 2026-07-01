using FluentValidation;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Application.Features.Common.Validators;

/// <summary>
/// Validates the <see cref="GetByKeyQuery{TEntity}"/> to ensure key values are provided.
/// </summary>
/// <typeparam name="TEntity">The type of the entity to query.</typeparam>
public class GetByKeyQueryValidator<TEntity> : AbstractValidator<GetByKeyQuery<TEntity>>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetByKeyQueryValidator{TEntity}"/> class.
    /// </summary>
    public GetByKeyQueryValidator()
    {
        RuleFor(x => x.CompositeKey)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Composite key must not be null.")
            .Must(k => k.Length > 0).WithMessage("Composite key must contain at least one value.")
            .ForEach(key => key
                .NotNull().WithMessage("Key values must not contain null elements."));
    }
}
