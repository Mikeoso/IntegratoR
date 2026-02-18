using FluentValidation;
using IntegratoR.Abstractions.Common.CQRS.Queries;

namespace IntegratoR.Application.Features.Common.Validators;

/// <summary>
/// Validates the <see cref="GetByFilterQuery{TEntity}"/> to ensure a filter expression is provided.
/// </summary>
public class GetByFilterQueryValidator<TEntity> : AbstractValidator<GetByFilterQuery<TEntity>>
    where TEntity : class
{
    public GetByFilterQueryValidator()
    {
        RuleFor(x => x.Filter)
            .NotNull().WithMessage("Filter expression must not be null.");
    }
}
