using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using Xunit;

namespace IntegratoR.Abstractions.Tests.Common.Results;

/// <summary>
/// Unit tests for <see cref="ResultExtensions"/> covering <c>GetError()</c> and <c>Match()</c>
/// for both generic and non-generic <see cref="Result"/> types.
/// </summary>
public sealed class ResultExtensionsTests
{
    // -----------------------------------------------------------------------
    // GetError() tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <c>GetError()</c> returns the first <see cref="IntegrationError"/> from a failed result.
    /// </summary>
    [Fact]
    public void GetError_ResultWithIntegrationError_ReturnsFirst()
    {
        // Arrange
        var error = new IntegrationError("CODE1", "First error", ErrorType.Failure);
        var result = Result.Fail(error);

        // Act
        var extracted = result.GetError();

        // Assert
        extracted.Should().NotBeNull();
        extracted!.Code.Should().Be("CODE1");
    }

    /// <summary>
    /// Verifies that <c>GetError()</c> returns the first <see cref="IntegrationError"/> when multiple exist.
    /// </summary>
    [Fact]
    public void GetError_ResultWithMultipleIntegrationErrors_ReturnsFirst()
    {
        // Arrange
        var first = new IntegrationError("FIRST", "First", ErrorType.Failure);
        var second = new IntegrationError("SECOND", "Second", ErrorType.Validation);
        var result = Result.Fail(new List<IError> { first, second });

        // Act
        var extracted = result.GetError();

        // Assert
        extracted.Should().NotBeNull();
        extracted!.Code.Should().Be("FIRST");
    }

    /// <summary>
    /// Verifies that <c>GetError()</c> returns null when the result contains no <see cref="IntegrationError"/>.
    /// </summary>
    [Fact]
    public void GetError_ResultWithNonIntegrationError_ReturnsNull()
    {
        // Arrange
        var result = Result.Fail("plain error message");

        // Act
        var extracted = result.GetError();

        // Assert
        extracted.Should().BeNull();
    }

    /// <summary>
    /// Verifies that <c>GetError()</c> returns null for a successful result.
    /// </summary>
    [Fact]
    public void GetError_SuccessResult_ReturnsNull()
    {
        // Arrange
        var result = Result.Ok();

        // Act
        var extracted = result.GetError();

        // Assert
        extracted.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Match<T, TOut>() (generic) tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <c>Match()</c> on a successful generic result invokes <c>onSuccess</c> with the value.
    /// </summary>
    [Fact]
    public void Match_SuccessResult_InvokesOnSuccessWithValue()
    {
        // Arrange
        var result = Result.Ok(42);

        // Act
        var output = result.Match(
            onSuccess: v => $"value={v}",
            onFailure: _ => "failed");

        // Assert
        output.Should().Be("value=42");
    }

    /// <summary>
    /// Verifies that <c>Match()</c> on a failed generic result with an <see cref="IntegrationError"/>
    /// invokes <c>onFailure</c> with the correct error.
    /// </summary>
    [Fact]
    public void Match_FailedWithIntegrationError_InvokesOnFailure()
    {
        // Arrange
        var error = new IntegrationError("ERR", "Bad thing", ErrorType.NotFound);
        var result = Result.Fail<int>(error);

        // Act
        var output = result.Match(
            onSuccess: _ => "ok",
            onFailure: e => $"error={e.Code}");

        // Assert
        output.Should().Be("error=ERR");
    }

    /// <summary>
    /// Verifies that <c>Match()</c> on a failed generic result without an <see cref="IntegrationError"/>
    /// creates a synthetic error with code "Unknown" and calls <c>onFailure</c>.
    /// </summary>
    [Fact]
    public void Match_FailedWithoutIntegrationError_CreatesSyntheticErrorWithUnknownCode()
    {
        // Arrange
        var result = Result.Fail<string>("some plain error");

        // Act
        IntegrationError? capturedError = null;
        var output = result.Match(
            onSuccess: _ => "ok",
            onFailure: e =>
            {
                capturedError = e;
                return "failed";
            });

        // Assert
        output.Should().Be("failed");
        capturedError.Should().NotBeNull();
        capturedError!.Code.Should().Be("Unknown");
        capturedError.Type.Should().Be(ErrorType.Failure);
    }

    // -----------------------------------------------------------------------
    // Match<TOut>() (non-generic) tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <c>Match()</c> on a successful non-generic result invokes <c>onSuccess</c>.
    /// </summary>
    [Fact]
    public void Match_SuccessNonGenericResult_InvokesOnSuccess()
    {
        // Arrange
        var result = Result.Ok();

        // Act
        var output = result.Match(
            onSuccess: () => "success",
            onFailure: _ => "failed");

        // Assert
        output.Should().Be("success");
    }

    /// <summary>
    /// Verifies that <c>Match()</c> on a failed non-generic result with an <see cref="IntegrationError"/>
    /// invokes <c>onFailure</c> with the correct error.
    /// </summary>
    [Fact]
    public void Match_FailedNonGenericWithIntegrationError_InvokesOnFailure()
    {
        // Arrange
        var error = new IntegrationError("CONFLICT", "Duplicate entity", ErrorType.Conflict);
        var result = Result.Fail(error);

        // Act
        var output = result.Match(
            onSuccess: () => "ok",
            onFailure: e => $"error={e.Code}:{e.Type}");

        // Assert
        output.Should().Be("error=CONFLICT:Conflict");
    }

    /// <summary>
    /// Verifies that <c>Match()</c> on a failed non-generic result without an <see cref="IntegrationError"/>
    /// creates a synthetic error with code "Unknown".
    /// </summary>
    [Fact]
    public void Match_FailedNonGenericWithoutIntegrationError_CreatesSyntheticErrorWithUnknownCode()
    {
        // Arrange
        var result = Result.Fail("unexpected failure");

        // Act
        IntegrationError? capturedError = null;
        var output = result.Match(
            onSuccess: () => "ok",
            onFailure: e =>
            {
                capturedError = e;
                return "failed";
            });

        // Assert
        output.Should().Be("failed");
        capturedError.Should().NotBeNull();
        capturedError!.Code.Should().Be("Unknown");
        capturedError.Message.Should().Be("unexpected failure");
        capturedError.Type.Should().Be(ErrorType.Failure);
    }
}
