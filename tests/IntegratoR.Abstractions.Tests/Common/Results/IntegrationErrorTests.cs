using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using Xunit;

namespace IntegratoR.Abstractions.Tests.Common.Results;

/// <summary>
/// Unit tests for <see cref="IntegrationError"/> covering construction with all parameter combinations.
/// </summary>
public sealed class IntegrationErrorTests
{
    /// <summary>
    /// Verifies that the constructor correctly sets <c>Code</c>, <c>Type</c>, and <c>Message</c>.
    /// </summary>
    [Fact]
    public void Constructor_WithAllParameters_SetsCodeTypeAndMessage()
    {
        // Arrange & Act
        var error = new IntegrationError("ERR001", "Something failed", ErrorType.Failure);

        // Assert
        error.Code.Should().Be("ERR001");
        error.Message.Should().Be("Something failed");
        error.Type.Should().Be(ErrorType.Failure);
    }

    /// <summary>
    /// Verifies that when an exception is provided, it is stored in <c>Exception</c>
    /// and the <c>Reasons</c> collection includes a <see cref="ExceptionalError"/>.
    /// </summary>
    [Fact]
    public void Constructor_WithException_SetsCausedByAndExceptionProperty()
    {
        // Arrange
        var exception = new InvalidOperationException("inner failure");

        // Act
        var error = new IntegrationError("ERR002", "Failed with exception", ErrorType.Failure, exception);

        // Assert
        error.Exception.Should().Be(exception);
        error.Reasons.Should().ContainSingle(r => r is ExceptionalError);
    }

    /// <summary>
    /// Verifies that when no exception is provided, the <c>Exception</c> property is null.
    /// </summary>
    [Fact]
    public void Constructor_WithoutException_ExceptionIsNull()
    {
        // Arrange & Act
        var error = new IntegrationError("ERR003", "No exception", ErrorType.NotFound);

        // Assert
        error.Exception.Should().BeNull();
        error.Reasons.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that all <see cref="ErrorType"/> values can be set correctly via the constructor.
    /// </summary>
    /// <param name="errorType">The error type to set.</param>
    [Theory]
    [InlineData(ErrorType.Failure)]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    public void Constructor_AllErrorTypes_SetsCorrectType(ErrorType errorType)
    {
        // Arrange & Act
        var error = new IntegrationError("CODE", "Message", errorType);

        // Assert
        error.Type.Should().Be(errorType);
    }
}
