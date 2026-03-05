using System.Net;
using FluentAssertions;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.Common.Services;
using IntegratoR.TestKit.Assertions;
using IntegratoR.TestKit.Builders;
using IntegratoR.TestKit.Doubles.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PanoramicData.OData.Client.Exceptions;
using Polly;
using Polly.Retry;
using Xunit;

namespace IntegratoR.OData.Tests.Common.Services;

/// <summary>
/// Tests for <see cref="ODataExceptionHandler{TEntity}"/> covering all exception mapping paths.
/// </summary>
public class ODataExceptionHandlerTests
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initialises a new instance with a mock logger.
    /// </summary>
    public ODataExceptionHandlerTests()
    {
        _logger = Substitute.For<ILogger>();
    }

    private ODataExceptionHandler<TestEntity> CreateHandler(AsyncRetryPolicy? retryPolicy = null)
        => new(_logger, retryPolicy);

    private static TestEntity CreateTestEntity()
        => TestEntityBuilder.Default().Build();

    /// <summary>
    /// Verifies that a successful operation returns a success result containing the entity.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SuccessfulOperation_ReturnsOkResult()
    {
        // Arrange
        var entity = CreateTestEntity();
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteAsync(
            "Test",
            () => Task.FromResult(entity),
            cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Id.Should().Be(entity.Id);
    }

    /// <summary>
    /// Verifies that with a retry policy, a transient failure is retried and succeeds on the second attempt.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithRetryPolicy_RetriesOnTransientFailure()
    {
        // Arrange
        var entity = CreateTestEntity();
        var callCount = 0;

        var retryPolicy = Policy
            .Handle<InvalidOperationException>()
            .WaitAndRetryAsync(1, _ => TimeSpan.Zero);

        var handler = CreateHandler(retryPolicy);

        // Act
        var result = await handler.ExecuteAsync(
            "Test",
            () =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("transient");
                return Task.FromResult(entity);
            },
            cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        callCount.Should().Be(2);
    }

    /// <summary>
    /// Verifies that without a retry policy, a single failure returns a failed result.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithoutRetryPolicy_ExecutesSingleAttempt()
    {
        // Arrange
        var callCount = 0;
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteAsync(
            "Test",
            () =>
            {
                callCount++;
                throw new InvalidOperationException("failure");
#pragma warning disable CS0162 // Unreachable code detected
                return Task.FromResult(CreateTestEntity());
#pragma warning restore CS0162
            },
            cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        callCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that ExecuteCollectionAsync returns a success result wrapping an IEnumerable of entities.
    /// </summary>
    [Fact]
    public async Task ExecuteCollectionAsync_Success_ReturnsOkWithEntities()
    {
        // Arrange
        var entities = new List<TestEntity> { CreateTestEntity(), CreateTestEntity() };
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteCollectionAsync(
            "Find",
            () => Task.FromResult<IEnumerable<TestEntity>>(entities),
            cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().HaveCount(2);
    }

    /// <summary>
    /// Verifies that ExecuteNonQueryAsync returns a non-generic Result.Ok() on success.
    /// </summary>
    [Fact]
    public async Task ExecuteNonQueryAsync_Success_ReturnsOkResult()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteNonQueryAsync(
            "Delete",
            () => Task.CompletedTask,
            cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
    }

    /// <summary>
    /// Verifies that ExecuteScalarAsync returns a success result wrapping the scalar value.
    /// </summary>
    [Fact]
    public async Task ExecuteScalarAsync_Success_ReturnsOkWithValue()
    {
        // Arrange
        const int count = 42;
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteScalarAsync(
            "Count",
            () => Task.FromResult(count),
            cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().Be(count);
    }

    /// <summary>
    /// Verifies that ODataUnauthorizedException maps to the Unauthorized error code.
    /// </summary>
    [Fact]
    public async Task HandleException_ODataUnauthorizedException_MapsToUnauthorizedError()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteAsync(
            "Test",
            () => throw new ODataUnauthorizedException("Unauthorized access"),
            cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.Unauthorized");
        result.Should().HaveErrorType(ErrorType.Failure);
    }

    /// <summary>
    /// Verifies that ODataForbiddenException maps to the Unauthorized error code.
    /// </summary>
    [Fact]
    public async Task HandleException_ODataForbiddenException_MapsToUnauthorizedError()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteAsync(
            "Test",
            () => throw new ODataForbiddenException("Forbidden access"),
            cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.Unauthorized");
        result.Should().HaveErrorType(ErrorType.Failure);
    }

    /// <summary>
    /// Verifies that ODataConcurrencyException maps to ConcurrencyConflict error.
    /// </summary>
    [Fact]
    public async Task HandleException_ODataConcurrencyException_MapsToConcurrencyConflictError()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteAsync(
            "Test",
            () => throw new ODataConcurrencyException("ETag mismatch", "https://test.example.com"),
            cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.ConcurrencyConflict");
        result.Should().HaveErrorType(ErrorType.Conflict);
    }

    /// <summary>
    /// Verifies that ODataClientException with various HTTP status codes maps to the correct IntegrationError code and type.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "TestEntity.ValidationFailed", ErrorType.Validation)]
    [InlineData(HttpStatusCode.NotFound, "TestEntity.NotFound", ErrorType.NotFound)]
    [InlineData(HttpStatusCode.Conflict, "TestEntity.Conflict", ErrorType.Conflict)]
    [InlineData(HttpStatusCode.TooManyRequests, "TestEntity.RateLimitExceeded", ErrorType.Failure)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "TestEntity.ServiceUnavailable", ErrorType.Failure)]
    [InlineData(HttpStatusCode.InternalServerError, "TestEntity.ServerError", ErrorType.Failure)]
    public async Task HandleException_ODataClientException_MapsToCorrectError(
        HttpStatusCode statusCode,
        string expectedErrorCode,
        ErrorType expectedType)
    {
        // Arrange
        var handler = CreateHandler();
        var exception = new ODataClientException($"HTTP {(int)statusCode} error", (int)statusCode, "", "https://test.example.com");

        // Act
        var result = await handler.ExecuteAsync(
            "Test",
            () => throw exception,
            cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode(expectedErrorCode);
        result.Should().HaveErrorType(expectedType);
    }

    /// <summary>
    /// Verifies that a TaskCanceledException with a non-cancelled token maps to a timeout error.
    /// </summary>
    [Fact]
    public async Task HandleException_TaskCanceledException_ReturnsTimeoutError()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteAsync(
            "Test",
            () => throw new TaskCanceledException("operation timed out"),
            cancellationToken: CancellationToken.None); // token is NOT cancelled

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.Timeout");
    }

    /// <summary>
    /// Verifies that an OperationCanceledException with a cancelled token maps to a cancelled error.
    /// </summary>
    [Fact]
    public async Task HandleException_OperationCanceledException_ReturnsCancelledError()
    {
        // Arrange
        var handler = CreateHandler();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var result = await handler.ExecuteAsync(
            "Test",
            () => throw new OperationCanceledException("operation was cancelled", cts.Token),
            cancellationToken: cts.Token); // token IS cancelled

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.Cancelled");
    }

    /// <summary>
    /// Verifies that an unexpected exception type maps to an unexpected error.
    /// </summary>
    [Fact]
    public async Task HandleException_UnexpectedException_ReturnsUnexpectedError()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteAsync(
            "Test",
            () => throw new InvalidOperationException("something went wrong"),
            cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.UnexpectedError");
    }

    /// <summary>
    /// Verifies that ODataNotFoundException with treatNotFoundAsSuccess=true returns Result.Ok().
    /// </summary>
    [Fact]
    public async Task ExecuteNonQueryAsync_NotFoundTreatAsSuccess_ReturnsOk()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteNonQueryAsync(
            "Delete",
            () => throw new ODataNotFoundException("entity not found"),
            cancellationToken: CancellationToken.None,
            treatNotFoundAsSuccess: true);

        // Assert
        result.Should().BeSuccessful();
    }

    /// <summary>
    /// Verifies that ODataNotFoundException with treatNotFoundAsSuccess=false returns a failed result.
    /// </summary>
    [Fact]
    public async Task ExecuteNonQueryAsync_NotFoundNotTreatAsSuccess_ReturnsNotFoundError()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteNonQueryAsync(
            "Delete",
            () => throw new ODataNotFoundException("entity not found"),
            cancellationToken: CancellationToken.None,
            treatNotFoundAsSuccess: false);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.NotFound");
        result.Should().HaveErrorType(ErrorType.NotFound);
    }
}
