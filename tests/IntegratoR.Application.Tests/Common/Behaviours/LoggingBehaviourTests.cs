using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
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

    /// <summary>
    /// A test request returning a generic <see cref="Result{T}"/>, used to prove the failure path
    /// matches via <see cref="IResultBase"/> (the old <c>is Result</c> check only matched non-generic).
    /// Carries a sensitive payload to verify the Information log never destructures the request body.
    /// </summary>
    public record TestSensitiveRequest(string Secret) : IRequest<Result<string>>, IContext
    {
        /// <summary>Returns logging context for this request.</summary>
        public IReadOnlyDictionary<string, object> GetLoggingContext() =>
            new Dictionary<string, object> { { "TestKey", "TestValue" } };
    }

    /// <summary>
    /// Renders every captured <see cref="ILogger.Log{TState}"/> call at <paramref name="level"/> to its
    /// final string by invoking the captured formatter delegate, so assertions can inspect the rendered
    /// message content rather than just the level.
    /// </summary>
    private static IEnumerable<string> RenderedMessages<TState, TResponse>(
        ILogger<LoggingBehaviour<TState, TResponse>> logger,
        LogLevel level)
        where TState : IRequest<TResponse>, IContext
    {
        foreach (var call in logger.ReceivedCalls())
        {
            if (call.GetMethodInfo().Name != nameof(ILogger.Log))
            {
                continue;
            }

            object?[] args = call.GetArguments();
            if (args[0] is not LogLevel callLevel || callLevel != level)
            {
                continue;
            }

            // args: [LogLevel, EventId, TState state, Exception?, Func<TState, Exception?, string> formatter]
            object? state = args[2];
            object? formatter = args[4];
            if (formatter is null)
            {
                continue;
            }

            string rendered = (string)formatter.GetType()
                .GetMethod("Invoke")!
                .Invoke(formatter, [state, args[3]])!;
            yield return rendered;
        }
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

        // Assert -- LoggingBehaviour emits: 1x Info (start), 1x Debug (request payload),
        // 1x Info (completion), 1x Debug (response) = 4 calls total.
        result.IsSuccess.Should().BeTrue();
        _logger.ReceivedWithAnyArgs(4).Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());

        // Two Information lines (start + completion) and two Debug lines (request payload + response).
        _logger.Received(2).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
        _logger.Received(2).Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
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

    /// <summary>
    /// Regression: a failed <see cref="Result{T}"/> (generic) must be logged as a Warning carrying the
    /// error code and message, and the success Information line must NOT be emitted. The pre-fix code
    /// matched only the non-generic <c>is Result</c>, so a failed <c>Result&lt;string&gt;</c> slipped
    /// through the success branch — this test fails on that code.
    /// </summary>
    [Fact]
    public async Task Handle_FailedResultOfT_LogsWarningWithErrorCodeAndMessage()
    {
        // Arrange
        var logger = Substitute.For<ILogger<LoggingBehaviour<TestSensitiveRequest, Result<string>>>>();
        var sut = new LoggingBehaviour<TestSensitiveRequest, Result<string>>(logger);
        var request = new TestSensitiveRequest("irrelevant");
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        next(Arg.Any<CancellationToken>())
            .Returns(Result.Fail<string>(new IntegrationError("Some.Code", "some message", ErrorType.Failure)));

        // Act
        var result = await sut.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();

        string[] warnings = RenderedMessages(logger, LogLevel.Warning).ToArray();
        warnings.Should().ContainSingle();
        warnings[0].Should().Contain("Some.Code").And.Contain("some message");

        // No "successfully" Information line should have been emitted.
        RenderedMessages(logger, LogLevel.Information)
            .Should().NotContain(message => message.Contains("successfully"));
    }

    /// <summary>
    /// The Information-level logs must never destructure the request body. The request payload is
    /// only emitted at Debug via <c>{@Request}</c>; a sensitive value carried on the request must
    /// not appear in any Information-rendered message.
    /// </summary>
    [Fact]
    public async Task Handle_InformationLog_DoesNotDestructureRequestBody()
    {
        // Arrange
        const string sentinel = "SENSITIVE-TOKEN";
        var logger = Substitute.For<ILogger<LoggingBehaviour<TestSensitiveRequest, Result<string>>>>();
        var sut = new LoggingBehaviour<TestSensitiveRequest, Result<string>>(logger);
        var request = new TestSensitiveRequest(sentinel);
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        next(Arg.Any<CancellationToken>()).Returns(Result.Ok("ok"));

        // Act
        await sut.Handle(request, next, CancellationToken.None);

        // Assert -- no Information-level rendered message contains the sentinel.
        RenderedMessages(logger, LogLevel.Information)
            .Should().NotContain(message => message.Contains(sentinel));
    }
}
