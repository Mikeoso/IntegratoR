using FluentAssertions;
using FluentResults;
using IntegratoR.RELion.Domain.Models;
using IntegratoR.RELion.Features.Queries.Ledger.GetLedgerAccountMapping;
using IntegratoR.RELion.Interfaces.Services;
using IntegratoR.TestKit.Assertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace IntegratoR.RELion.Tests.Features.Queries.Ledger.GetLedgerAccountMapping;

/// <summary>
/// Tests for <see cref="GetRelionLedgerAccountMappingHandler"/> verifying delegation to
/// <see cref="IRelionService"/> and result propagation.
/// </summary>
public class GetRelionLedgerAccountMappingHandlerTests
{
    private readonly ILogger<GetRelionLedgerAccountMappingHandler> _logger =
        Substitute.For<ILogger<GetRelionLedgerAccountMappingHandler>>();

    private readonly IRelionService _relionService = Substitute.For<IRelionService>();
    private readonly GetRelionLedgerAccountMappingHandler _sut;

    /// <summary>
    /// Initialises the handler under test with mocked dependencies.
    /// </summary>
    public GetRelionLedgerAccountMappingHandlerTests()
    {
        _sut = new GetRelionLedgerAccountMappingHandler(_logger, _relionService);
    }

    /// <summary>
    /// Verifies that when the service returns a successful result, the handler returns the same success.
    /// </summary>
    [Fact]
    public async Task Handle_ServiceReturnsSuccess_ReturnsSuccessWithMapping()
    {
        // Arrange
        var mapping = new RelionLedgerAccountMapping
        {
            LedgerAccountNo = "GL001",
            TaxAccountNo = "VAT001"
        };
        var query = new GetRelionLedgerAccountMappingQuery(42);
        _relionService.GetLedgerAccountMappingsAsync(42, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(mapping));

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.LedgerAccountNo.Should().Be("GL001");
        result.Value.TaxAccountNo.Should().Be("VAT001");
    }

    /// <summary>
    /// Verifies that when the service returns a failure, the handler propagates the failure.
    /// </summary>
    [Fact]
    public async Task Handle_ServiceReturnsFailure_ReturnsFailure()
    {
        // Arrange
        var query = new GetRelionLedgerAccountMappingQuery(99);
        _relionService.GetLedgerAccountMappingsAsync(99, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<RelionLedgerAccountMapping>("Service.Failed"));

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
    }

    /// <summary>
    /// Verifies that the handler calls the service with the correct EntryNo from the query.
    /// </summary>
    [Fact]
    public async Task Handle_DelegatesToRelionServiceWithCorrectEntryNo()
    {
        // Arrange
        var mapping = new RelionLedgerAccountMapping
        {
            LedgerAccountNo = string.Empty,
            TaxAccountNo = string.Empty
        };
        var query = new GetRelionLedgerAccountMappingQuery(77);
        _relionService.GetLedgerAccountMappingsAsync(77, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(mapping));

        // Act
        await _sut.Handle(query, CancellationToken.None);

        // Assert
        await _relionService.Received(1).GetLedgerAccountMappingsAsync(77, Arg.Any<CancellationToken>());
    }
}
