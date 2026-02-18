using FluentValidation.TestHelper;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;
using Xunit;

namespace IntegratoR.OData.FO.Tests.Features.Queries.Dimensions.GetDimensionOrder;

/// <summary>
/// Tests for <see cref="GetDimensionOrdersQueryValidator"/> covering all validation rules.
/// </summary>
public class GetDimensionOrdersQueryValidatorTests
{
    private readonly GetDimensionOrdersQueryValidator _sut = new();

    /// <summary>
    /// Verifies that a valid query passes all validation rules.
    /// </summary>
    [Fact]
    public void Validate_ValidQuery_HasNoErrors()
    {
        // Arrange
        var query = new GetDimensionOrdersQuery(
            "DefaultFormat",
            DimensionHierarchyType.DataEntityDefaultDimensionFormat);

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Verifies that an empty dimensionFormat triggers a NotEmpty validation error.
    /// </summary>
    [Fact]
    public void Validate_EmptyDimensionFormat_HasError()
    {
        // Arrange
        var query = new GetDimensionOrdersQuery(
            "",
            DimensionHierarchyType.DataEntityDefaultDimensionFormat);

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(q => q.dimensionFormat);
    }

    /// <summary>
    /// Verifies that a dimensionFormat exceeding 100 characters triggers a MaximumLength validation error.
    /// </summary>
    [Fact]
    public void Validate_DimensionFormatExceeds100Chars_HasError()
    {
        // Arrange
        var longFormat = new string('A', 101);
        var query = new GetDimensionOrdersQuery(
            longFormat,
            DimensionHierarchyType.DataEntityDefaultDimensionFormat);

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(q => q.dimensionFormat);
    }

    /// <summary>
    /// Verifies that an invalid hierarchy type value triggers an IsInEnum validation error.
    /// </summary>
    [Fact]
    public void Validate_InvalidHierarchyType_HasError()
    {
        // Arrange
        var query = new GetDimensionOrdersQuery(
            "DefaultFormat",
            (DimensionHierarchyType)999);

        // Act
        var result = _sut.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(q => q.hierarchyType);
    }
}
