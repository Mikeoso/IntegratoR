using FluentAssertions;
using IntegratoR.OData.FO.Common.Extensions;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using Xunit;

namespace IntegratoR.OData.FO.Tests.Common.Extensions;

/// <summary>
/// Tests for <see cref="DimensionSegmentDelimiterExtensions.GetCharValue"/> covering supported values and unsupported edge cases.
/// </summary>
public class DimensionSegmentDelimiterExtensionsTests
{
    /// <summary>
    /// Verifies that GetCharValue returns the hyphen character for the Hyphen enum value.
    /// </summary>
    [Fact]
    public void GetCharValue_Hyphen_ReturnsHyphenChar()
    {
        // Arrange
        DimensionSegmentDelimiter? delimiter = DimensionSegmentDelimiter.Hyphen;

        // Act
        var result = delimiter.GetCharValue();

        // Assert
        result.Should().Be('-');
    }

    /// <summary>
    /// Verifies that GetCharValue throws ArgumentOutOfRangeException for unsupported enum values.
    /// The current implementation only handles Hyphen; all other values fall through to the default case.
    /// </summary>
    [Theory]
    [InlineData(DimensionSegmentDelimiter.Period)]
    [InlineData(DimensionSegmentDelimiter.Underscore)]
    [InlineData(DimensionSegmentDelimiter.Bar)]
    public void GetCharValue_UnsupportedEnum_ThrowsArgumentOutOfRangeException(DimensionSegmentDelimiter unsupported)
    {
        // Arrange
        DimensionSegmentDelimiter? delimiter = unsupported;

        // Act
        var act = () => delimiter.GetCharValue();

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that GetCharValue throws ArgumentOutOfRangeException when called with a null value.
    /// </summary>
    [Fact]
    public void GetCharValue_Null_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        DimensionSegmentDelimiter? delimiter = null;

        // Act
        var act = () => delimiter.GetCharValue();

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
