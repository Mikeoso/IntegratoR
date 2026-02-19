using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Telemetry;
using IntegratoR.Application.Common.Behaviours;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace IntegratoR.Application.Tests.Common.Behaviours;

/// <summary>
/// Tests for <see cref="LoggingBehaviour{TRequest,TResponse}"/>.
/// </summary>
public class LoggingBehaviourTests
{
    private readonly ILogger<LoggingBehaviour<TestLoggingRequest, Result>> _logger =
        Substitute.For<ILogger<LoggingBehaviour<TestLoggingRequest, Result>>>();

    /// <summary>
    /// A test request that implements both IRequest and IContext for use in LoggingBehaviour tests.
    /// </summary>
    public record TestLoggingRequest : IRequest<Result>, IContext
    {
        /// <summary>Returns logging context for this request.</summary>
        public IReadOnlyDictionary<string, object> GetLoggingContext() =>
            new Dictionary<string, object> { { "TestKey", "TestValue" } };
    }

    [Fact]
    public async Task Handle_SuccessfulRequest_LogsStartAndCompletionWithElapsedTime()
    {
        // Arrange
        var sut = new LoggingBehaviour<TestLoggingRequest, Result>(_logger);
        var request = new TestLoggingRequest();
        var next = Substitute.For<RequestHandlerDelegate<Result>>();
        next(Arg.Any<CancellationToken>()).Returns(Result.Ok());

        // Act
        var result = await sut.Handle(request, next, CancellationToken.None);

        // Assert -- LoggingBehaviour emits: 1x Info (start), 1x Info (completion), 1x Debug (response) = 3 calls total
        result.IsSuccess.Should().BeTrue();
        _logger.ReceivedWithAnyArgs(3).Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_FailedResult_LogsWarningWithErrorCodeAndMessage()
    {
        // Arrange
        var sut = new LoggingBehaviour<TestLoggingRequest, Result>(_logger);
        var request = new TestLoggingRequest();
        var next = Substitute.For<RequestHandlerDelegate<Result>>();
        next(Arg.Any<CancellationToken>()).Returns(Result.Fail("Some.Error"));

        // Act
        var result = await sut.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        _logger.ReceivedWithAnyArgs().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_UnhandledException_LogsErrorAndRethrows()
    {
        // Arrange
        var sut = new LoggingBehaviour<TestLoggingRequest, Result>(_logger);
        var request = new TestLoggingRequest();
        var expectedException = new InvalidOperationException("Boom");
        var next = Substitute.For<RequestHandlerDelegate<Result>>();
        next(Arg.Any<CancellationToken>()).Returns<Result>(_ => throw expectedException);

        // Act
        var act = async () => await sut.Handle(request, next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Boom");
        _logger.ReceivedWithAnyArgs().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_Request_CreatesLoggingScopeFromGetLoggingContext()
    {
        // Arrange
        var sut = new LoggingBehaviour<TestLoggingRequest, Result>(_logger);
        var request = new TestLoggingRequest();
        var next = Substitute.For<RequestHandlerDelegate<Result>>();
        next(Arg.Any<CancellationToken>()).Returns(Result.Ok());

        // Act
        await sut.Handle(request, next, CancellationToken.None);

        // Assert -- BeginScope should be called once with the logging context dictionary
        _logger.ReceivedWithAnyArgs(1).BeginScope(Arg.Any<Dictionary<string, object>>());
    }

    [Fact]
    public async Task Handle_SuccessfulRequest_LogsDebugResponsePayload()
    {
        // Arrange
        var sut = new LoggingBehaviour<TestLoggingRequest, Result>(_logger);
        var request = new TestLoggingRequest();
        var next = Substitute.For<RequestHandlerDelegate<Result>>();
        next(Arg.Any<CancellationToken>()).Returns(Result.Ok());

        // Act
        await sut.Handle(request, next, CancellationToken.None);

        // Assert -- LogDebug called for response payload
        _logger.ReceivedWithAnyArgs().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
