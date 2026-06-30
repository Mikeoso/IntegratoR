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
    /// Security: a 401 must map to a fixed, generic error message that does NOT echo the underlying
    /// exception detail (tenant IDs, AADSTS codes, MSAL specifics). The message is the constant
    /// "Authentication or authorisation failure" — the full detail stays server-side in the log only.
    /// </summary>
    [Fact]
    public async Task HandleException_Unauthorized_MessageIsGenericAndOmitsExceptionDetail()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteAsync(
            "Test",
            () => throw new ODataUnauthorizedException("AADSTS500011 tenant 1111-2222-3333 directory not found"),
            cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.GetError()!.Message.Should().Be("Authentication or authorisation failure");
        result.GetError()!.Message.Should().NotContain("AADSTS");
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

    /// <summary>
    /// Regression: when PanoramicData raises an HTTP 404 (not the internal ODataNotFoundException)
    /// and the caller opted into treatNotFoundAsSuccess, the handler must still return success
    /// — AND log a Warning that includes the request URL so operators can distinguish a genuine
    /// not-found from a malformed-URL silent failure. This was the behaviour that let
    /// LNR0000266–LNR0000269 leak into the JFI sandbox during the 2026-04-14 smoke test.
    /// </summary>
    [Fact]
    public async Task ExecuteNonQueryAsync_HttpNotFoundTreatAsSuccess_ReturnsOkAndLogsWarningWithUrl()
    {
        // Arrange
        var handler = CreateHandler();
        const string requestUrl = "https://test.example.com/TestEntities(BadKey)";
        var exception = new ODataClientException("Not found", 404, "", requestUrl);

        // Act
        var result = await handler.ExecuteNonQueryAsync(
            "Delete",
            () => throw exception,
            cancellationToken: CancellationToken.None,
            treatNotFoundAsSuccess: true);

        // Assert
        result.Should().BeSuccessful();

        // Inspect the underlying Log<TState> call directly — LogWarning dispatches through
        // Log<FormattedLogValues>, which NSubstitute cannot match via Arg.Is<object>(...)
        // because the closed generic type is different. ReceivedCalls() is type-agnostic.
        var logCall = _logger.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(ILogger.Log));
        object?[] args = logCall.GetArguments();
        args[0].Should().Be(LogLevel.Warning);
        args[2]!.ToString().Should().Contain(requestUrl);
    }

    /// <summary>
    /// Pins that an HTTP 404 WITHOUT treatNotFoundAsSuccess still produces a failed Result
    /// (so the warning-suppression path does not leak into the normal error-reporting path).
    /// </summary>
    [Fact]
    public async Task ExecuteNonQueryAsync_HttpNotFoundNotTreatAsSuccess_ReturnsNotFoundError()
    {
        // Arrange
        var handler = CreateHandler();
        var exception = new ODataClientException("Not found", 404, "", "https://test.example.com/TestEntities(123)");

        // Act
        var result = await handler.ExecuteNonQueryAsync(
            "Delete",
            () => throw exception,
            cancellationToken: CancellationToken.None,
            treatNotFoundAsSuccess: false);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.NotFound");
        result.Should().HaveErrorType(ErrorType.NotFound);
    }

    #region ExtractD365InnerError Tests

    /// <summary>
    /// Verifies that ExtractD365InnerError extracts the innererror.message from a D365 error response.
    /// </summary>
    [Fact]
    public void ExtractD365InnerError_WithInnerErrorMessage_ReturnsInnerMessage()
    {
        // Arrange
        const string responseBody = """{"error":{"message":"Bad request","innererror":{"message":"Field 'Amount' is required"}}}""";

        // Act
        string? result = ODataExceptionHandler<TestEntity>.ExtractD365InnerError(responseBody);

        // Assert
        result.Should().Be("Field 'Amount' is required");
    }

    /// <summary>
    /// Verifies that ExtractD365InnerError falls back to error.message when innererror is absent.
    /// </summary>
    [Fact]
    public void ExtractD365InnerError_WithOnlyErrorMessage_ReturnsErrorMessage()
    {
        // Arrange
        const string responseBody = """{"error":{"message":"Entity not found"}}""";

        // Act
        string? result = ODataExceptionHandler<TestEntity>.ExtractD365InnerError(responseBody);

        // Assert
        result.Should().Be("Entity not found");
    }

    /// <summary>
    /// Verifies that ExtractD365InnerError returns null for null/empty input.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractD365InnerError_NullOrEmpty_ReturnsNull(string? responseBody)
    {
        // Act
        string? result = ODataExceptionHandler<TestEntity>.ExtractD365InnerError(responseBody);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that ExtractD365InnerError returns null for non-JSON content.
    /// </summary>
    [Fact]
    public void ExtractD365InnerError_InvalidJson_ReturnsNull()
    {
        // Act
        string? result = ODataExceptionHandler<TestEntity>.ExtractD365InnerError("<html>Not JSON</html>");

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Regression: APIM sometimes returns malformed JSON error bodies where unescaped quotes
    /// inside the error string break <c>JsonDocument.Parse</c>. The lenient fallback must still
    /// extract the human-readable message so the consumer-visible error is meaningful rather
    /// than the generic "Request failed with status ...". Observed 2026-04-14 against the
    /// IntegratoR sandbox APIM when the <c>d365foenvironment</c> header value was wrong.
    /// </summary>
    [Fact]
    public void ExtractD365InnerError_MalformedJsonFromApim_ReturnsLenientExtraction()
    {
        // Arrange — exactly the body observed in the smoke-test session
        const string responseBody = """{"error": "The provided value for "d365foenvironment" is not valid. Please refer to the provided api documentation"}""";

        // Act
        string? result = ODataExceptionHandler<TestEntity>.ExtractD365InnerError(responseBody);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("d365foenvironment");
        result.Should().Contain("not valid");
    }

    /// <summary>
    /// Direct unit test for the lenient extractor helper — pins the behaviour independent of
    /// the JsonDocument fallback path.
    /// </summary>
    [Theory]
    [InlineData("""{"error": "simple"}""", "simple")]
    [InlineData("""{"message": "inner"}""", "inner")]
    [InlineData("""{"error": "has "inner" quotes"}""", """has "inner" quotes""")]
    // Regression: when "error" is not the last property, the extractor must stop at the
    // first structural delimiter (",") and not over-capture into subsequent properties.
    [InlineData("""{"error": "x", "details": "y"}""", "x")]
    [InlineData("""{"message": "primary", "code": "E42"}""", "primary")]
    // Precedence: when both "error" and "message" are present, the more specific "message"
    // key wins because D365 wraps the human-readable text under error.innererror.message.
    [InlineData("""{"error": {"code": "BadRequest", "message": "inner text"}}""", "inner text")]
    public void ExtractLenientErrorMessage_ReturnsValue(string body, string expected)
    {
        string? result = ODataExceptionHandler<TestEntity>.ExtractLenientErrorMessage(body);

        result.Should().Be(expected);
    }

    /// <summary>
    /// The lenient extractor must return null when the body has no recognisable error/message
    /// key. This pins that a garbage body does not produce a misleading "error" to the consumer.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<html>nothing</html>")]
    [InlineData("just random text no braces")]
    public void ExtractLenientErrorMessage_NoRecognisableKey_ReturnsNull(string body)
    {
        string? result = ODataExceptionHandler<TestEntity>.ExtractLenientErrorMessage(body);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that ExtractD365InnerError returns null when JSON has no error property.
    /// </summary>
    [Fact]
    public void ExtractD365InnerError_NoErrorProperty_ReturnsNull()
    {
        // Act
        string? result = ODataExceptionHandler<TestEntity>.ExtractD365InnerError("""{"status":"ok"}""");

        // Assert
        result.Should().BeNull();
    }

    #endregion
}
