using FluentValidation;

namespace IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;

/// <summary>
/// Validates <see cref="GetDimensionOrdersQuery"/>, requiring a non-empty dimension format name of
/// at most 100 characters and a valid hierarchy type.
/// </summary>
public class GetDimensionOrdersQueryValidator : AbstractValidator<GetDimensionOrdersQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetDimensionOrdersQueryValidator"/> class.
    /// </summary>
    public GetDimensionOrdersQueryValidator()
    {
        RuleFor(x => x.DimensionFormat)
            .NotEmpty().WithMessage("Dimension format must be provided.")
            .MaximumLength(100).WithMessage("Dimension format must not exceed 100 characters.");
        RuleFor(x => x.HierarchyType)
            .IsInEnum().WithMessage("Hierarchy type must be a valid enum value.");
    }
}
