using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;
using IntegratoR.OData.Interfaces.Services;
using IntegratoR.TestKit.Assertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace IntegratoR.OData.FO.Tests.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;

/// <summary>
/// Tests for <see cref="CreateLedgerJournalHeaderHandler{TEntity}"/> and <see cref="CreateLedgerJournalHeadersHandler{TEntity}"/>
/// covering success and failure paths for single and batch create operations.
/// </summary>
public class CreateLedgerJournalHeaderHandlerTests
{
    private static LedgerJournalHeader BuildHeader() => new()
    {
        DataAreaId = "USMF",
        JournalBatchNumber = "GJ001",
        JournalName = "GenJnl",
        Description = "Test journal"
    };

    /// <summary>
    /// Verifies that the single handler returns a success result with the entity when AddAsync succeeds.
    /// </summary>
    [Fact]
    public async Task Handle_SingleHeader_Success_ReturnsOkWithEntity()
    {
        // Arrange
        var service = Substitute.For<IService<LedgerJournalHeader>>();
        var logger = Substitute.For<ILogger<CreateLedgerJournalHeaderHandler<LedgerJournalHeader>>>();
        var handler = new CreateLedgerJournalHeaderHandler<LedgerJournalHeader>(logger, service);

        var header = BuildHeader();
        service.AddAsync(header, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(header));

        var command = new CreateLedgerJournalHeaderCommand<LedgerJournalHeader>(header);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Should().HaveValue(header);
    }

    /// <summary>
    /// Verifies that the single handler propagates failure when AddAsync returns an error.
    /// </summary>
    [Fact]
    public async Task Handle_SingleHeader_Failure_ReturnsError()
    {
        // Arrange
        var service = Substitute.For<IService<LedgerJournalHeader>>();
        var logger = Substitute.For<ILogger<CreateLedgerJournalHeaderHandler<LedgerJournalHeader>>>();
        var handler = new CreateLedgerJournalHeaderHandler<LedgerJournalHeader>(logger, service);

        var header = BuildHeader();
        var error = new IntegrationError("LedgerJournalHeader.CreateFailed", "Service error", ErrorType.Failure);
        service.AddAsync(header, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<LedgerJournalHeader>(error));

        var command = new CreateLedgerJournalHeaderCommand<LedgerJournalHeader>(header);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("LedgerJournalHeader.CreateFailed");
    }

    /// <summary>
    /// Verifies that the batch handler returns a success result when AddBatchAsync succeeds.
    /// </summary>
    [Fact]
    public async Task Handle_BatchHeaders_Success_ReturnsOk()
    {
        // Arrange
        var service = Substitute.For<IODataBatchService<LedgerJournalHeader>>();
        var logger = Substitute.For<ILogger<CreateLedgerJournalHeadersHandler<LedgerJournalHeader>>>();
        var handler = new CreateLedgerJournalHeadersHandler<LedgerJournalHeader>(logger, service);

        var headers = new[] { BuildHeader(), BuildHeader() };
        service.AddBatchAsync(headers, Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        var command = new CreateLedgerJournalHeadersCommand<LedgerJournalHeader>(headers);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
    }

    /// <summary>
    /// Verifies that the batch handler propagates failure when AddBatchAsync returns an error.
    /// </summary>
    [Fact]
    public async Task Handle_BatchHeaders_Failure_ReturnsError()
    {
        // Arrange
        var service = Substitute.For<IODataBatchService<LedgerJournalHeader>>();
        var logger = Substitute.For<ILogger<CreateLedgerJournalHeadersHandler<LedgerJournalHeader>>>();
        var handler = new CreateLedgerJournalHeadersHandler<LedgerJournalHeader>(logger, service);

        var headers = new[] { BuildHeader() };
        var error = new IntegrationError("LedgerJournalHeader.BatchCreateFailed", "Batch service error", ErrorType.Failure);
        service.AddBatchAsync(headers, Arg.Any<CancellationToken>())
            .Returns(Result.Fail(error));

        var command = new CreateLedgerJournalHeadersCommand<LedgerJournalHeader>(headers);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
    }
}
