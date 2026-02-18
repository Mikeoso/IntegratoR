using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalLine;
using IntegratoR.OData.Interfaces.Services;
using IntegratoR.TestKit.Assertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace IntegratoR.OData.FO.Tests.Features.Commands.LedgerJournals.UpdateLedgerJournalLine;

/// <summary>
/// Tests for <see cref="UpdateLedgerJournalLineHandler{TEntity}"/> and <see cref="UpdateLedgerJournalLinesHandler{TEntity}"/>
/// covering success and failure paths for single and batch update operations.
/// </summary>
public class UpdateLedgerJournalLineHandlerTests
{
    private static LedgerJournalLine BuildLine() => new()
    {
        DataAreaId = "USMF",
        JournalBatchNumber = "GJ001",
        LineNumber = 1.0m,
        AccountDisplayValue = "110110-001-023",
        CurrencyCode = "USD",
        TransDate = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Verifies that the single handler returns a success result with the entity when UpdateAsync succeeds.
    /// </summary>
    [Fact]
    public async Task Handle_SingleLine_Success_ReturnsOkWithEntity()
    {
        // Arrange
        var service = Substitute.For<IService<LedgerJournalLine>>();
        var logger = Substitute.For<ILogger<UpdateLedgerJournalLineHandler<LedgerJournalLine>>>();
        var handler = new UpdateLedgerJournalLineHandler<LedgerJournalLine>(logger, service);

        var line = BuildLine();
        service.UpdateAsync(line, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(line));

        var command = new UpdateLedgerJournalLineCommand<LedgerJournalLine>(line);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Should().HaveValue(line);
    }

    /// <summary>
    /// Verifies that the single handler propagates failure when UpdateAsync returns an error.
    /// </summary>
    [Fact]
    public async Task Handle_SingleLine_Failure_ReturnsError()
    {
        // Arrange
        var service = Substitute.For<IService<LedgerJournalLine>>();
        var logger = Substitute.For<ILogger<UpdateLedgerJournalLineHandler<LedgerJournalLine>>>();
        var handler = new UpdateLedgerJournalLineHandler<LedgerJournalLine>(logger, service);

        var line = BuildLine();
        var error = new IntegrationError("LedgerJournalLine.UpdateFailed", "Service error", ErrorType.Failure);
        service.UpdateAsync(line, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<LedgerJournalLine>(error));

        var command = new UpdateLedgerJournalLineCommand<LedgerJournalLine>(line);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("LedgerJournalLine.UpdateFailed");
    }

    /// <summary>
    /// Verifies that the batch handler returns a success result when UpdateBatchAsync succeeds.
    /// </summary>
    [Fact]
    public async Task Handle_BatchLines_Success_ReturnsOk()
    {
        // Arrange
        var service = Substitute.For<IODataBatchService<LedgerJournalLine>>();
        var logger = Substitute.For<ILogger<UpdateLedgerJournalLinesHandler<LedgerJournalLine>>>();
        var handler = new UpdateLedgerJournalLinesHandler<LedgerJournalLine>(logger, service);

        var lines = new[] { BuildLine(), BuildLine() };
        service.UpdateBatchAsync(lines, Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        var command = new UpdateLedgerJournalLinesCommand<LedgerJournalLine>(lines);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
    }

    /// <summary>
    /// Verifies that the batch handler propagates failure when UpdateBatchAsync returns an error.
    /// </summary>
    [Fact]
    public async Task Handle_BatchLines_Failure_ReturnsError()
    {
        // Arrange
        var service = Substitute.For<IODataBatchService<LedgerJournalLine>>();
        var logger = Substitute.For<ILogger<UpdateLedgerJournalLinesHandler<LedgerJournalLine>>>();
        var handler = new UpdateLedgerJournalLinesHandler<LedgerJournalLine>(logger, service);

        var lines = new[] { BuildLine() };
        var error = new IntegrationError("LedgerJournalLine.BatchUpdateFailed", "Batch service error", ErrorType.Failure);
        service.UpdateBatchAsync(lines, Arg.Any<CancellationToken>())
            .Returns(Result.Fail(error));

        var command = new UpdateLedgerJournalLinesCommand<LedgerJournalLine>(lines);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
    }
}
