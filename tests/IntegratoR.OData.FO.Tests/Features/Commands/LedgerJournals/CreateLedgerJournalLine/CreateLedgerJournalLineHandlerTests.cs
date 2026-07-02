using FluentResults;
using IntegratoR.Abstractions.Common.Batch;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;
using IntegratoR.OData.Interfaces.Services;
using IntegratoR.TestKit.Assertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace IntegratoR.OData.FO.Tests.Features.Commands.LedgerJournals.CreateLedgerJournalLine;

/// <summary>
/// Tests for <see cref="CreateLedgerJournalLineHandler{TEntity}"/> and <see cref="CreateLedgerJournalLinesHandler{TEntity}"/>
/// covering success and failure paths for single and batch create operations.
/// </summary>
public class CreateLedgerJournalLineHandlerTests
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
    /// Verifies that the single handler returns a success result with the entity when AddAsync succeeds.
    /// </summary>
    [Fact]
    public async Task Handle_SingleLine_Success_ReturnsOkWithEntity()
    {
        // Arrange
        var service = Substitute.For<IService<LedgerJournalLine>>();
        var logger = Substitute.For<ILogger<CreateLedgerJournalLineHandler<LedgerJournalLine>>>();
        var handler = new CreateLedgerJournalLineHandler<LedgerJournalLine>(logger, service);

        var line = BuildLine();
        service.AddAsync(line, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(line));

        var command = new CreateLedgerJournalLineCommand<LedgerJournalLine>(line);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Should().HaveValue(line);
    }

    /// <summary>
    /// Verifies that the single handler propagates failure when AddAsync returns an error.
    /// </summary>
    [Fact]
    public async Task Handle_SingleLine_Failure_ReturnsError()
    {
        // Arrange
        var service = Substitute.For<IService<LedgerJournalLine>>();
        var logger = Substitute.For<ILogger<CreateLedgerJournalLineHandler<LedgerJournalLine>>>();
        var handler = new CreateLedgerJournalLineHandler<LedgerJournalLine>(logger, service);

        var line = BuildLine();
        var error = new IntegrationError("LedgerJournalLine.CreateFailed", "Service error", ErrorType.Failure);
        service.AddAsync(line, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<LedgerJournalLine>(error));

        var command = new CreateLedgerJournalLineCommand<LedgerJournalLine>(line);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("LedgerJournalLine.CreateFailed");
    }

    /// <summary>
    /// Verifies that the batch handler returns a success result when AddBatchAsync succeeds.
    /// </summary>
    [Fact]
    public async Task Handle_BatchLines_Success_ReturnsOk()
    {
        // Arrange
        var service = Substitute.For<IODataBatchService<LedgerJournalLine>>();
        var logger = Substitute.For<ILogger<CreateLedgerJournalLinesHandler<LedgerJournalLine>>>();
        var handler = new CreateLedgerJournalLinesHandler<LedgerJournalLine>(logger, service);

        var lines = new[] { BuildLine(), BuildLine() };
        service.AddBatchAsync(lines, Arg.Any<BatchOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new BatchOutcome { Mode = BatchFailureMode.Atomic, ChunkCount = 1, Items = [] }));

        var command = new CreateLedgerJournalLinesCommand<LedgerJournalLine>(lines);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
    }

    /// <summary>
    /// Verifies that the batch handler propagates failure when AddBatchAsync returns an error.
    /// </summary>
    [Fact]
    public async Task Handle_BatchLines_Failure_ReturnsError()
    {
        // Arrange
        var service = Substitute.For<IODataBatchService<LedgerJournalLine>>();
        var logger = Substitute.For<ILogger<CreateLedgerJournalLinesHandler<LedgerJournalLine>>>();
        var handler = new CreateLedgerJournalLinesHandler<LedgerJournalLine>(logger, service);

        var lines = new[] { BuildLine() };
        var error = new IntegrationError("LedgerJournalLine.BatchCreateFailed", "Batch service error", ErrorType.Failure);
        service.AddBatchAsync(lines, Arg.Any<BatchOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<BatchOutcome>(error));

        var command = new CreateLedgerJournalLinesCommand<LedgerJournalLine>(lines);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
    }
}
