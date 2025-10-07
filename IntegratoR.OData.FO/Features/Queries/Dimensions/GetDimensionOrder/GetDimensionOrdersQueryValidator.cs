using FluentValidation;

namespace IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;

public class GetDimensionOrdersQueryValidator : AbstractValidator<GetDimensionOrdersQuery>
{
    public GetDimensionOrdersQueryValidator()
    {
        RuleFor(x => x.dimensionFormat)
            .NotEmpty().WithMessage("Dimension format must be provided.")
            .MaximumLength(100).WithMessage("Dimension format must not exceed 100 characters.");
        RuleFor(x => x.hierarchyType)
            .IsInEnum().WithMessage("Hierarchy type must be a valid enum value.");
    }
}