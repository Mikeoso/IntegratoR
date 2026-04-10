using FluentAssertions;
using IntegratoR.OData.FO.Builders;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;
using IntegratoR.TestKit.Assertions;
using Xunit;

namespace IntegratoR.OData.FO.Tests.Builders;

/// <summary>
/// Tests for <see cref="FinancialDimensionReader"/> covering parsing, error handling, and round-trip with the builder.
/// </summary>
public class FinancialDimensionReaderTests
{
    /// <summary>
    /// Verifies that Parse maps all segment values correctly when every segment is present.
    /// </summary>
    [Fact]
    public void Parse_AllSegmentsPresent_ReturnsDictionary()
    {
        // Arrange
        var format = new DimensionFormat
        {
            Delimiter = "-",
            Segments = new List<string> { "BU", "Dept", "CC" }
        };

        // Act
        var result = FinancialDimensionReader.Parse(format, "BU01-D001-CC002");

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().HaveCount(3);
        result.Value["BU"].Should().Be("BU01");
        result.Value["Dept"].Should().Be("D001");
        result.Value["CC"].Should().Be("CC002");
    }

    /// <summary>
    /// Verifies that a missing middle segment is included as an empty string.
    /// </summary>
    [Fact]
    public void Parse_EmptyMiddleSegment_IncludesEmptyValue()
    {
        // Arrange
        var format = new DimensionFormat
        {
            Delimiter = "-",
            Segments = new List<string> { "BU", "Dept", "CC" }
        };

        // Act
        var result = FinancialDimensionReader.Parse(format, "BU01--CC002");

        // Assert
        result.Should().BeSuccessful();
        result.Value["BU"].Should().Be("BU01");
        result.Value["Dept"].Should().Be(string.Empty);
        result.Value["CC"].Should().Be("CC002");
    }

    /// <summary>
    /// Verifies that a dimension string with all empty values returns all segments mapped to empty strings.
    /// </summary>
    [Fact]
    public void Parse_AllSegmentsEmpty_ReturnsAllEmptyValues()
    {
        // Arrange
        var format = new DimensionFormat
        {
            Delimiter = "-",
            Segments = new List<string> { "BU", "Dept", "CC" }
        };

        // Act
        var result = FinancialDimensionReader.Parse(format, "--");

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().HaveCount(3);
        result.Value.Values.Should().AllBe(string.Empty);
    }

    /// <summary>
    /// Verifies that a single-segment format parses a value without delimiters.
    /// </summary>
    [Fact]
    public void Parse_SingleSegment_ReturnsOneEntry()
    {
        // Arrange
        var format = new DimensionFormat
        {
            Delimiter = "-",
            Segments = new List<string> { "MainAccount" }
        };

        // Act
        var result = FinancialDimensionReader.Parse(format, "110110");

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().HaveCount(1);
        result.Value["MainAccount"].Should().Be("110110");
    }

    /// <summary>
    /// Verifies that Parse fails when the dimension string has more segments than the format defines.
    /// </summary>
    [Fact]
    public void Parse_MoreSegmentsThanFormat_ReturnsFailure()
    {
        // Arrange
        var format = new DimensionFormat
        {
            Delimiter = "-",
            Segments = new List<string> { "BU", "Dept", "CC" }
        };

        // Act
        var result = FinancialDimensionReader.Parse(format, "BU01-D001-CC002-Extra");

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("DimensionReader.SegmentCountMismatch");
    }

    /// <summary>
    /// Verifies that Parse fails when the dimension string has fewer segments than the format defines.
    /// </summary>
    [Fact]
    public void Parse_FewerSegmentsThanFormat_ReturnsFailure()
    {
        // Arrange
        var format = new DimensionFormat
        {
            Delimiter = "-",
            Segments = new List<string> { "BU", "Dept", "CC" }
        };

        // Act
        var result = FinancialDimensionReader.Parse(format, "BU01-D001");

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("DimensionReader.SegmentCountMismatch");
    }

    /// <summary>
    /// Verifies that Parse fails when the format is null.
    /// </summary>
    [Fact]
    public void Parse_NullFormat_ReturnsFailure()
    {
        // Act
        var result = FinancialDimensionReader.Parse(null!, "BU01-D001");

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("DimensionReader.InvalidFormat");
    }

    /// <summary>
    /// Verifies that Parse fails when the format has no segments.
    /// </summary>
    [Fact]
    public void Parse_EmptySegmentsFormat_ReturnsFailure()
    {
        // Arrange
        var format = new DimensionFormat
        {
            Delimiter = "-",
            Segments = new List<string>()
        };

        // Act
        var result = FinancialDimensionReader.Parse(format, "BU01");

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("DimensionReader.InvalidFormat");
    }

    /// <summary>
    /// Verifies that Parse fails when the dimension string is null.
    /// </summary>
    [Fact]
    public void Parse_NullDimensionString_ReturnsFailure()
    {
        // Arrange
        var format = new DimensionFormat
        {
            Delimiter = "-",
            Segments = new List<string> { "BU" }
        };

        // Act
        var result = FinancialDimensionReader.Parse(format, null!);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("DimensionReader.EmptyInput");
    }

    /// <summary>
    /// Verifies that Parse fails when the dimension string is empty.
    /// </summary>
    [Fact]
    public void Parse_EmptyDimensionString_ReturnsFailure()
    {
        // Arrange
        var format = new DimensionFormat
        {
            Delimiter = "-",
            Segments = new List<string> { "BU" }
        };

        // Act
        var result = FinancialDimensionReader.Parse(format, string.Empty);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("DimensionReader.EmptyInput");
    }

    /// <summary>
    /// Verifies that parsing the output of <see cref="FinancialDimensionBuilder"/> produces
    /// the original segment values, proving lossless round-tripping.
    /// </summary>
    [Fact]
    public void Parse_RoundTrip_MatchesBuilderOutput()
    {
        // Arrange
        var format = new DimensionFormat
        {
            Delimiter = "-",
            Segments = new List<string> { "BU", "Dept", "CC" }
        };

        var builder = new FinancialDimensionBuilder();
        string built = builder
            .Initialize(format)
            .Add("CC", "CC002")
            .Add("BU", "BU01")
            .Build();

        // Act — parse the builder's output
        var result = FinancialDimensionReader.Parse(format, built);

        // Assert
        result.Should().BeSuccessful();
        result.Value["BU"].Should().Be("BU01");
        result.Value["Dept"].Should().Be(string.Empty);
        result.Value["CC"].Should().Be("CC002");
    }
}
