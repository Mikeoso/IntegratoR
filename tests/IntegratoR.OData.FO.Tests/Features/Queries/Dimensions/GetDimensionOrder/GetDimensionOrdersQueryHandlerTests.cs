using System.Linq.Expressions;
using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.Dimensions;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Domain.Enums.General;
using IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;
using IntegratoR.OData.Interfaces.Services;
using IntegratoR.TestKit.Assertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace IntegratoR.OData.FO.Tests.Features.Queries.Dimensions.GetDimensionOrder;

/// <summary>
/// Tests for <see cref="GetDimensionOrdersQueryHandler"/> covering success, failure, and edge case paths.
/// </summary>
public class GetDimensionOrdersQueryHandlerTests
{
    private readonly IODataService<DimensionParameters> _dimParamsService;
    private readonly IService<DimensionIntegrationFormat> _dimFormatService;
    private readonly ILogger<GetDimensionOrdersQueryHandler> _logger;
    private readonly GetDimensionOrdersQueryHandler _sut;

    /// <summary>
    /// Initialises a new instance with mocked services and logger.
    /// </summary>
    public GetDimensionOrdersQueryHandlerTests()
    {
        _dimParamsService = Substitute.For<IODataService<DimensionParameters>>();
        _dimFormatService = Substitute.For<IService<DimensionIntegrationFormat>>();
        _logger = Substitute.For<ILogger<GetDimensionOrdersQueryHandler>>();
        _sut = new GetDimensionOrdersQueryHandler(_logger, _dimParamsService, _dimFormatService);
    }

    /// <summary>
    /// Verifies that Handle returns a DimensionFormat with correct delimiter and segment list when both service calls succeed.
    /// </summary>
    [Fact]
    public async Task Handle_ValidRequest_ReturnsDimensionFormatWithSegmentsAndDelimiter()
    {
        // Arrange
        var dimParams = new DimensionParameters
        {
            Key = 0,
            DimensionSegmentDelimiter = DimensionSegmentDelimiter.Hyphen
        };
        _dimParamsService.FindAll(Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IEnumerable<DimensionParameters>>(new[] { dimParams }));

        var dimFormat = new DimensionIntegrationFormat
        {
            DimensionFormatName = "DefaultFormat",
            DimensionFormatType = DimensionHierarchyType.DataEntityDefaultDimensionFormat,
            FinancialDimensionFormat = "MainAccount-BU-Dept",
            IsActive = NoYes.Yes
        };
        _dimFormatService.FindAsync(Arg.Any<Expression<Func<DimensionIntegrationFormat, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IEnumerable<DimensionIntegrationFormat>>(new[] { dimFormat }));

        var query = new GetDimensionOrdersQuery(
            "DefaultFormat",
            DimensionHierarchyType.DataEntityDefaultDimensionFormat);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Delimiter.Should().Be("-");
        result.Value.Segments.Should().Equal("MainAccount", "BU", "Dept");
    }

    /// <summary>
    /// Verifies that Handle returns a failure result when FindAsync for dimension formats fails.
    /// </summary>
    [Fact]
    public async Task Handle_DimensionFormatsQueryFails_ReturnsFailure()
    {
        // Arrange
        var error = new IntegrationError("DimensionParameters.QueryFailed", "Service unavailable", ErrorType.Failure);
        _dimFormatService.FindAsync(Arg.Any<Expression<Func<DimensionIntegrationFormat, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IEnumerable<DimensionIntegrationFormat>>(error));

        var query = new GetDimensionOrdersQuery(
            "DefaultFormat",
            DimensionHierarchyType.DataEntityDefaultDimensionFormat);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("DimensionParameters.QueryFailed");
    }

    /// <summary>
    /// Verifies that Handle returns a failure result when FindAll for dimension parameters fails.
    /// </summary>
    [Fact]
    public async Task Handle_DimensionParametersQueryFails_ReturnsFailure()
    {
        // Arrange
        var dimFormat = new DimensionIntegrationFormat
        {
            DimensionFormatName = "DefaultFormat",
            DimensionFormatType = DimensionHierarchyType.DataEntityDefaultDimensionFormat,
            FinancialDimensionFormat = "MainAccount-BU-Dept",
            IsActive = NoYes.Yes
        };
        _dimFormatService.FindAsync(Arg.Any<Expression<Func<DimensionIntegrationFormat, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IEnumerable<DimensionIntegrationFormat>>(new[] { dimFormat }));

        var error = new IntegrationError("DimensionParameters.QueryFailed", "Service unavailable", ErrorType.Failure);
        _dimParamsService.FindAll(Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IEnumerable<DimensionParameters>>(error));

        var query = new GetDimensionOrdersQuery(
            "DefaultFormat",
            DimensionHierarchyType.DataEntityDefaultDimensionFormat);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("DimensionParameters.QueryFailed");
    }

    /// <summary>
    /// Verifies that Handle processes gracefully when the dimension formats collection is empty.
    /// When FindAsync returns an empty collection, FirstOrDefault returns null, and the handler uses
    /// an empty segment list due to null-conditional split: "null?.Split() ?? new List&lt;string&gt;()".
    /// </summary>
    [Fact]
    public async Task Handle_NoDimensionFormats_ReturnsEmptySegments()
    {
        // Arrange
        var dimParams = new DimensionParameters
        {
            Key = 0,
            DimensionSegmentDelimiter = DimensionSegmentDelimiter.Hyphen
        };
        _dimParamsService.FindAll(Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IEnumerable<DimensionParameters>>(new[] { dimParams }));

        _dimFormatService.FindAsync(Arg.Any<Expression<Func<DimensionIntegrationFormat, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IEnumerable<DimensionIntegrationFormat>>(Enumerable.Empty<DimensionIntegrationFormat>()));

        var query = new GetDimensionOrdersQuery(
            "DefaultFormat",
            DimensionHierarchyType.DataEntityDefaultDimensionFormat);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert -- handler returns success but with empty segments (null?.Split() falls back to empty list)
        result.Should().BeSuccessful();
        result.Value.Segments.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that Handle correctly parses a FinancialDimensionFormat string into individual segments.
    /// </summary>
    [Fact]
    public async Task Handle_ParsesFinancialDimensionFormatString_IntoSegments()
    {
        // Arrange
        var dimParams = new DimensionParameters
        {
            Key = 0,
            DimensionSegmentDelimiter = DimensionSegmentDelimiter.Hyphen
        };
        _dimParamsService.FindAll(Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IEnumerable<DimensionParameters>>(new[] { dimParams }));

        var dimFormat = new DimensionIntegrationFormat
        {
            DimensionFormatName = "DefaultFormat",
            DimensionFormatType = DimensionHierarchyType.DataEntityDefaultDimensionFormat,
            FinancialDimensionFormat = "MainAccount-BU-Dept",
            IsActive = NoYes.Yes
        };
        _dimFormatService.FindAsync(Arg.Any<Expression<Func<DimensionIntegrationFormat, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IEnumerable<DimensionIntegrationFormat>>(new[] { dimFormat }));

        var query = new GetDimensionOrdersQuery(
            "DefaultFormat",
            DimensionHierarchyType.DataEntityDefaultDimensionFormat);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Segments.Should().HaveCount(3);
        result.Value.Segments[0].Should().Be("MainAccount");
        result.Value.Segments[1].Should().Be("BU");
        result.Value.Segments[2].Should().Be("Dept");
    }
}
