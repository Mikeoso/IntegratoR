using FluentAssertions;
using IntegratoR.OData.FO.Builders;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;
using Xunit;

namespace IntegratoR.OData.FO.Tests.Builders;

/// <summary>
/// Tests for <see cref="FinancialDimensionBuilder"/> covering all segment ordering, delimiter joining, and state reset scenarios.
/// </summary>
public class FinancialDimensionBuilderTests
{
    private readonly FinancialDimensionBuilder _sut = new();

    /// <summary>
    /// Verifies that Build returns an empty string when Initialize has not been called.
    /// </summary>
    [Fact]
    public void Build_NotInitialized_ReturnsEmptyString()
    {
        // Act
        var result = _sut.Build();

        // Assert
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that Build joins all provided segment values with the configured delimiter.
    /// </summary>
    [Fact]
    public void Build_AllSegmentsProvided_JoinsWithDelimiter()
    {
        // Arrange
        var format = new DimensionFormat
        {
            Delimiter = "-",
            Segments = new List<string> { "BU", "Dept", "CC" }
        };

        _sut.Initialize(format)
            .Add("BU", "BU01")
            .Add("Dept", "D001")
            .Add("CC", "CC002");

        // Act
        var result = _sut.Build();

        // Assert
        result.Should().Be("BU01-D001-CC002");
    }

    /// <summary>
    /// Verifies that Build inserts an empty placeholder for a missing middle segment.
    /// </summary>
    [Fact]
    public void Build_MissingMiddleSegment_InsertsEmptyPlaceholder()
    {
        // Arrange
        var format = new DimensionFormat
        {
            Delimiter = "-",
            Segments = new List<string> { "BU", "Dept", "CC" }
        };

        _sut.Initialize(format)
            .Add("BU", "BU01")
            .Add("CC", "CC002");

        // Act
        var result = _sut.Build();

        // Assert
        result.Should().Be("BU01--CC002");
    }

    /// <summary>
    /// Verifies that Build respects format segment order even when dimensions are added out of order.
    /// </summary>
    [Fact]
    public void Build_AddedOutOfOrder_RespectsFormatOrder()
    {
        // Arrange
        var format = new DimensionFormat
        {
            Delimiter = "-",
            Segments = new List<string> { "BU", "Dept", "CC" }
        };

        _sut.Initialize(format)
            .Add("CC", "CC002")
            .Add("BU", "BU01");

        // Act
        var result = _sut.Build();

        // Assert
        result.Should().Be("BU01--CC002");
    }

    /// <summary>
    /// Verifies that Build returns a value with no delimiter when the format has only one segment.
    /// </summary>
    [Fact]
    public void Build_SingleSegment_NoDelimiter()
    {
        // Arrange
        var format = new DimensionFormat
        {
            Delimiter = "-",
            Segments = new List<string> { "MainAccount" }
        };

        _sut.Initialize(format)
            .Add("MainAccount", "110110");

        // Act
        var result = _sut.Build();

        // Assert
        result.Should().Be("110110");
        result.Should().NotContain("-");
    }

    /// <summary>
    /// Verifies that Add ignores entries with null or whitespace names or values.
    /// </summary>
    [Fact]
    public void Add_NullOrWhitespaceName_IgnoresEntry()
    {
        // Arrange
        var format = new DimensionFormat
        {
            Delimiter = "-",
            Segments = new List<string> { "BU", "Dept" }
        };

        _sut.Initialize(format)
            .Add(null!, "value")
            .Add("  ", "value")
            .Add("BU", "  ")
            .Add("Dept", "D001");

        // Act
        var result = _sut.Build();

        // Assert -- only Dept has a value, BU is empty placeholder
        result.Should().Be("-D001");
    }

    /// <summary>
    /// Verifies that Clear resets all state so that Build returns empty string.
    /// </summary>
    [Fact]
    public void Clear_AfterAdditions_ResetsState()
    {
        // Arrange
        var format = new DimensionFormat
        {
            Delimiter = "-",
            Segments = new List<string> { "BU", "Dept" }
        };

        _sut.Initialize(format)
            .Add("BU", "BU01")
            .Add("Dept", "D001");

        // Act
        _sut.Clear();
        var result = _sut.Build();

        // Assert
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that Initialize after prior use clears old segments and applies new format.
    /// </summary>
    [Fact]
    public void Initialize_AfterPreviousUse_ClearsState()
    {
        // Arrange -- first use
        var firstFormat = new DimensionFormat
        {
            Delimiter = "-",
            Segments = new List<string> { "OldSegment" }
        };
        _sut.Initialize(firstFormat).Add("OldSegment", "OldValue");

        // Arrange -- second use with different format
        var newFormat = new DimensionFormat
        {
            Delimiter = "_",
            Segments = new List<string> { "NewSegment" }
        };

        // Act
        _sut.Initialize(newFormat).Add("NewSegment", "NewValue");
        var result = _sut.Build();

        // Assert -- old segments are gone, new format applies
        result.Should().Be("NewValue");
        result.Should().NotContain("OldValue");
    }
}
