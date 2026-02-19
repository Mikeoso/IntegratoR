using FluentAssertions;
using IntegratoR.OData.FO.Domain.Entities.Dimensions;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using Xunit;

namespace IntegratoR.OData.FO.Tests.Domain.Entities.Dimensions;

/// <summary>
/// Tests for dimension entity composite keys: <see cref="DimensionIntegrationFormat"/> and <see cref="DimensionParameters"/>.
/// </summary>
public class DimensionEntityTests
{
    /// <summary>
    /// Verifies that DimensionIntegrationFormat.GetCompositeKey returns DimensionFormatName and DimensionFormatType.
    /// </summary>
    [Fact]
    public void DimensionIntegrationFormat_GetCompositeKey_ReturnsFormatNameAndType()
    {
        // Arrange
        var entity = new DimensionIntegrationFormat
        {
            DimensionFormatName = "DefaultFormat",
            DimensionFormatType = DimensionHierarchyType.DataEntityDefaultDimensionFormat
        };

        // Act
        var key = entity.GetCompositeKey();

        // Assert
        key.Should().HaveCount(2);
        key[0].Should().Be("DefaultFormat");
        key[1].Should().Be(DimensionHierarchyType.DataEntityDefaultDimensionFormat);
    }

    /// <summary>
    /// Verifies that DimensionParameters.GetCompositeKey returns the Key field.
    /// </summary>
    [Fact]
    public void DimensionParameters_GetCompositeKey_ReturnsKey()
    {
        // Arrange
        var entity = new DimensionParameters
        {
            Key = "Default"
        };

        // Act
        var key = entity.GetCompositeKey();

        // Assert
        key.Should().HaveCount(1);
        key[0].Should().Be("Default");
    }
}
