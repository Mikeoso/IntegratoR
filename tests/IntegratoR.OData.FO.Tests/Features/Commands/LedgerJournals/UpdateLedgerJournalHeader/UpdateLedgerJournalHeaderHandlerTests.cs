using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalHeader;
using IntegratoR.OData.Interfaces.Services;
using IntegratoR.TestKit.Assertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace IntegratoR.OData.FO.Tests.Features.Commands.LedgerJournals.UpdateLedgerJournalHeader;

/// <summary>
/// Tests for <see cref="UpdateLedgerJournalHeaderHandler{TEntity}"/> and <see cref="UpdateLedgerJournalHeadersHandler{TEntity}"/>
/// covering success and failure paths for single and batch update operations.
/// </summary>
public class UpdateLedgerJournalHeaderHandlerTests
{
    private static LedgerJournalHeader BuildHeader() => new()
    {
        DataAreaId = "USMF",
        JournalBatchNumber = "GJ001",
        JournalName = "GenJnl",
        Description = "Test journal"
    };

    /// <summary>
    /// Verifies that the single handler returns a success result with the entity when UpdateAsync succeeds.
    /// </summary>
    [Fact]
    public async Task Handle_SingleHeader_Success_ReturnsOkWithEntity()
    {
        // Arrange
        var service = Substitute.For<IService<LedgerJournalHeader>>();
        var logger = Substitute.For<ILogger<UpdateLedgerJournalHeaderHandler<LedgerJournalHeader>>>();
        var handler = new UpdateLedgerJournalHeaderHandler<LedgerJournalHeader>(logger, service);

        var header = BuildHeader();
        service.UpdateAsync(header, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(header));

        var command = new UpdateLedgerJournalHeaderCommand<LedgerJournalHeader>(header);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Should().HaveValue(header);
    }

    /// <summary>
    /// Verifies that the single handler propagates failure when UpdateAsync returns an error.
    /// </summary>
    [Fact]
    public async Task Handle_SingleHeader_Failure_ReturnsError()
    {
        // Arrange
        var service = Substitute.For<IService<LedgerJournalHeader>>();
        var logger = Substitute.For<ILogger<UpdateLedgerJournalHeaderHandler<LedgerJournalHeader>>>();
        var handler = new UpdateLedgerJournalHeaderHandler<LedgerJournalHeader>(logger, service);

        var header = BuildHeader();
        var error = new IntegrationError("LedgerJournalHeader.UpdateFailed", "Service error", ErrorType.Failure);
        service.UpdateAsync(header, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<LedgerJournalHeader>(error));

        var command = new UpdateLedgerJournalHeaderCommand<LedgerJournalHeader>(header);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("LedgerJournalHeader.UpdateFailed");
    }

    /// <summary>
    /// Verifies that the batch handler returns a success result when UpdateBatchAsync succeeds.
    /// </summary>
    [Fact]
    public async Task Handle_BatchHeaders_Success_ReturnsOk()
    {
        // Arrange
        var service = Substitute.For<IODataBatchService<LedgerJournalHeader>>();
        var logger = Substitute.For<ILogger<UpdateLedgerJournalHeadersHandler<LedgerJournalHeader>>>();
        var handler = new UpdateLedgerJournalHeadersHandler<LedgerJournalHeader>(logger, service);

        var headers = new[] { BuildHeader(), BuildHeader() };
        service.UpdateBatchAsync(headers, Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        var command = new UpdateLedgerJournalHeadersCommand<LedgerJournalHeader>(headers);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
    }

    /// <summary>
    /// Verifies that the batch handler propagates failure when UpdateBatchAsync returns an error.
    /// </summary>
    [Fact]
    public async Task Handle_BatchHeaders_Failure_ReturnsError()
    {
        // Arrange
        var service = Substitute.For<IODataBatchService<LedgerJournalHeader>>();
        var logger = Substitute.For<ILogger<UpdateLedgerJournalHeadersHandler<LedgerJournalHeader>>>();
        var handler = new UpdateLedgerJournalHeadersHandler<LedgerJournalHeader>(logger, service);

        var headers = new[] { BuildHeader() };
        var error = new IntegrationError("LedgerJournalHeader.BatchUpdateFailed", "Batch service error", ErrorType.Failure);
        service.UpdateBatchAsync(headers, Arg.Any<CancellationToken>())
            .Returns(Result.Fail(error));

        var command = new UpdateLedgerJournalHeadersCommand<LedgerJournalHeader>(headers);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
    }
}
