using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.Application.Common.Behaviours;
using IntegratoR.TestKit.Fakes;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace IntegratoR.Application.Tests.Common.Behaviours;

/// <summary>
/// Tests for <see cref="CachingBehaviour{TRequest,TResponse}"/>.
/// </summary>
public class CachingBehaviourTests
{
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly ILogger<CachingBehaviour<TestCacheableQuery<Result<string>>, Result<string>>> _logger =
        Substitute.For<ILogger<CachingBehaviour<TestCacheableQuery<Result<string>>, Result<string>>>>();

    /// <summary>A plain request that does NOT implement ICacheableQuery.</summary>
    public record NonCacheableRequest : IRequest<Result<string>>;

    [Fact]
    public async Task Handle_NonCacheableQuery_PassesThroughWithoutCacheInteraction()
    {
        // Arrange
        var cacheService = Substitute.For<ICacheService>();
        var logger = Substitute.For<ILogger<CachingBehaviour<NonCacheableRequest, Result<string>>>>();
        var sut = new CachingBehaviour<NonCacheableRequest, Result<string>>(cacheService, logger);
        var request = new NonCacheableRequest();
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        next(Arg.Any<CancellationToken>()).Returns(Result.Ok("value"));

        // Act
        var result = await sut.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await next.Received(1)(Arg.Any<CancellationToken>());
        await cacheService.DidNotReceiveWithAnyArgs().GetAsync<Result<string>>(default!);
        await cacheService.DidNotReceiveWithAnyArgs().SetAsync(default!, default(Result<string>)!, default);
    }

    [Fact]
    public async Task Handle_CacheHit_ReturnsCachedResponse()
    {
        // Arrange
        var sut = new CachingBehaviour<TestCacheableQuery<Result<string>>, Result<string>>(_cacheService, _logger);
        var cachedResult = Result.Ok("cached-value");
        var request = new TestCacheableQuery<Result<string>>("test-key", TimeSpan.FromMinutes(5));
        _cacheService.GetAsync<Result<string>>("test-key").Returns(cachedResult);
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();

        // Act
        var result = await sut.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("cached-value");
        await next.DidNotReceive()(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CacheMiss_ExecutesHandlerAndCachesSuccessfulResult()
    {
        // Arrange
        var sut = new CachingBehaviour<TestCacheableQuery<Result<string>>, Result<string>>(_cacheService, _logger);
        var request = new TestCacheableQuery<Result<string>>("test-key", TimeSpan.FromMinutes(5));
        _cacheService.GetAsync<Result<string>>("test-key").Returns((Result<string>?)null);
        var expected = Result.Ok("fresh-value");
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        next(Arg.Any<CancellationToken>()).Returns(expected);

        // Act
        var result = await sut.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await next.Received(1)(Arg.Any<CancellationToken>());
        await _cacheService.Received(1).SetAsync("test-key", expected, TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Handle_CacheMiss_FailedResult_DoesNotCache()
    {
        // Arrange
        var sut = new CachingBehaviour<TestCacheableQuery<Result<string>>, Result<string>>(_cacheService, _logger);
        var request = new TestCacheableQuery<Result<string>>("test-key", TimeSpan.FromMinutes(5));
        _cacheService.GetAsync<Result<string>>("test-key").Returns((Result<string>?)null);
        var failed = Result.Fail<string>("Some.Error");
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        next(Arg.Any<CancellationToken>()).Returns(failed);

        // Act
        var result = await sut.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        await _cacheService.DidNotReceiveWithAnyArgs().SetAsync(default!, default(Result<string>)!, default);
    }

    [Fact]
    public async Task Handle_CacheHit_LogsDebugCacheHit()
    {
        // Arrange
        var sut = new CachingBehaviour<TestCacheableQuery<Result<string>>, Result<string>>(_cacheService, _logger);
        var cachedResult = Result.Ok("cached");
        var request = new TestCacheableQuery<Result<string>>("test-key", TimeSpan.FromMinutes(5));
        _cacheService.GetAsync<Result<string>>("test-key").Returns(cachedResult);
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();

        // Act
        await sut.Handle(request, next, CancellationToken.None);

        // Assert
        _logger.ReceivedWithAnyArgs().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_CacheMiss_LogsDebugCacheMiss()
    {
        // Arrange
        var sut = new CachingBehaviour<TestCacheableQuery<Result<string>>, Result<string>>(_cacheService, _logger);
        var request = new TestCacheableQuery<Result<string>>("test-key", TimeSpan.FromMinutes(5));
        _cacheService.GetAsync<Result<string>>("test-key").Returns((Result<string>?)null);
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        next(Arg.Any<CancellationToken>()).Returns(Result.Ok("value"));

        // Act
        await sut.Handle(request, next, CancellationToken.None);

        // Assert
        _logger.ReceivedWithAnyArgs().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_CacheMiss_SuccessfulResult_SetsCorrectCacheDuration()
    {
        // Arrange
        var expectedDuration = TimeSpan.FromMinutes(15);
        var sut = new CachingBehaviour<TestCacheableQuery<Result<string>>, Result<string>>(_cacheService, _logger);
        var request = new TestCacheableQuery<Result<string>>("test-key", expectedDuration);
        _cacheService.GetAsync<Result<string>>("test-key").Returns((Result<string>?)null);
        var response = Result.Ok("value");
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        next(Arg.Any<CancellationToken>()).Returns(response);

        // Act
        await sut.Handle(request, next, CancellationToken.None);

        // Assert
        await _cacheService.Received(1).SetAsync("test-key", response, expectedDuration);
    }
}
