using FluentValidation;
using IntegratoR.Abstractions.Common.CQRS.Queries;

namespace IntegratoR.Application.Features.Common.Validators;

/// <summary>
/// Validates the <see cref="GetByFilterQuery{TEntity}"/> to ensure a filter expression is provided.
/// </summary>
/// <typeparam name="TEntity">The type of the entity to query.</typeparam>
public class GetByFilterQueryValidator<TEntity> : AbstractValidator<GetByFilterQuery<TEntity>>
    where TEntity : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetByFilterQueryValidator{TEntity}"/> class.
    /// </summary>
    public GetByFilterQueryValidator()
    {
        RuleFor(x => x.Filter)
            .NotNull().WithMessage("Filter expression must not be null.");
    }
}
