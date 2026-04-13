using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using Xunit;

namespace IntegratoR.Abstractions.Tests.Common.Results;

/// <summary>
/// Tests for the serializer-agnostic <see cref="ResultJsonShape"/> helper. The Newtonsoft.Json
/// and System.Text.Json converters both delegate their IError ↔ primitives mapping here, so
/// these tests cover the mapping behaviour once for both serialisers.
/// </summary>
public sealed class ResultJsonShapeTests
{
    /// <summary>
    /// A non-IntegrationError IError implementation used to verify that Project rejects unknown error types.
    /// </summary>
    private sealed class CustomError : Error
    {
        public CustomError(string message) : base(message) { }
    }

    /// <summary>
    /// Verifies that Project flattens an IntegrationError into its three serialisation primitives.
    /// </summary>
    [Fact]
    public void Project_IntegrationError_ReturnsCodeMessageAndType()
    {
        // Arrange
        var error = new IntegrationError("OData.NotFound", "Customer 'C001' not found.", ErrorType.NotFound);

        // Act
        (string code, string message, ErrorType type) = ResultJsonShape.Project(error);

        // Assert
        code.Should().Be("OData.NotFound");
        message.Should().Be("Customer 'C001' not found.");
        type.Should().Be(ErrorType.NotFound);
    }

    /// <summary>
    /// Verifies that Project falls back to (Unknown, error.Message, Failure) for any non-IntegrationError.
    /// The Newtonsoft converters in this assembly are public API and have always accepted any IError;
    /// preserving that contract avoids an unannounced breaking change for downstream callers.
    /// </summary>
    [Fact]
    public void Project_NonIntegrationError_FallsBackToUnknownAndFailure()
    {
        // Arrange
        var customError = new CustomError("Something failed");

        // Act
        (string code, string message, ErrorType type) = ResultJsonShape.Project(customError);

        // Assert
        code.Should().Be("Unknown");
        message.Should().Be("Something failed");
        type.Should().Be(ErrorType.Failure);
    }

    /// <summary>
    /// Verifies that Project falls back to "Unknown error" when a non-IntegrationError has an empty message.
    /// </summary>
    [Fact]
    public void Project_NonIntegrationErrorWithEmptyMessage_FallsBackToUnknownError()
    {
        // Arrange
        var customError = new CustomError(string.Empty);

        // Act
        (string code, string message, ErrorType type) = ResultJsonShape.Project(customError);

        // Assert
        code.Should().Be("Unknown");
        message.Should().Be("Unknown error");
        type.Should().Be(ErrorType.Failure);
    }

    /// <summary>
    /// Verifies that Hydrate reconstructs an IntegrationError when all fields are present.
    /// </summary>
    [Fact]
    public void Hydrate_AllFieldsPresent_ConstructsIntegrationError()
    {
        // Act
        IntegrationError reconstructed = ResultJsonShape.Hydrate(
            "OData.NotFound",
            "Customer 'C001' not found.",
            "NotFound");

        // Assert
        reconstructed.Code.Should().Be("OData.NotFound");
        reconstructed.Message.Should().Be("Customer 'C001' not found.");
        reconstructed.Type.Should().Be(ErrorType.NotFound);
    }

    /// <summary>
    /// Verifies that Hydrate applies fallback values when individual fields are null.
    /// </summary>
    [Fact]
    public void Hydrate_NullCode_FallsBackToUnknown()
    {
        // Act
        IntegrationError reconstructed = ResultJsonShape.Hydrate(null, "msg", "Failure");

        // Assert
        reconstructed.Code.Should().Be("Unknown");
        reconstructed.Message.Should().Be("msg");
    }

    /// <summary>
    /// Verifies that Hydrate applies the unknown-message fallback when the message is null.
    /// </summary>
    [Fact]
    public void Hydrate_NullMessage_FallsBackToUnknownError()
    {
        // Act
        IntegrationError reconstructed = ResultJsonShape.Hydrate("Code", null, "Failure");

        // Assert
        reconstructed.Code.Should().Be("Code");
        reconstructed.Message.Should().Be("Unknown error");
    }

    /// <summary>
    /// Verifies that Hydrate falls back to ErrorType.Failure when the type string is null.
    /// </summary>
    [Fact]
    public void Hydrate_NullType_FallsBackToFailure()
    {
        // Act
        IntegrationError reconstructed = ResultJsonShape.Hydrate("Code", "msg", null);

        // Assert
        reconstructed.Type.Should().Be(ErrorType.Failure);
    }

    /// <summary>
    /// Verifies that Hydrate falls back to ErrorType.Failure when the type string is not a valid enum value.
    /// </summary>
    [Fact]
    public void Hydrate_InvalidTypeString_FallsBackToFailure()
    {
        // Act
        IntegrationError reconstructed = ResultJsonShape.Hydrate("Code", "msg", "NotAValidErrorType");

        // Assert
        reconstructed.Type.Should().Be(ErrorType.Failure);
    }

    /// <summary>
    /// Verifies that Hydrate parses the type string case-insensitively. Without case insensitivity,
    /// JSON produced by a different serialiser (e.g. <c>"notFound"</c>) would silently demote to
    /// ErrorType.Failure, defeating the cross-serialiser compatibility goal.
    /// </summary>
    [Theory]
    [InlineData("NotFound", ErrorType.NotFound)]
    [InlineData("notfound", ErrorType.NotFound)]
    [InlineData("NOTFOUND", ErrorType.NotFound)]
    [InlineData("Validation", ErrorType.Validation)]
    [InlineData("validation", ErrorType.Validation)]
    [InlineData("VALIDATION", ErrorType.Validation)]
    public void Hydrate_TypeStringCaseInsensitive_ParsesCorrectly(string typeString, ErrorType expected)
    {
        // Act
        IntegrationError reconstructed = ResultJsonShape.Hydrate("Code", "msg", typeString);

        // Assert
        reconstructed.Type.Should().Be(expected);
    }

    /// <summary>
    /// Verifies that <see cref="ResultJsonShape.MissingValueError"/> returns a stable error
    /// shape that converters can use when a successful Result is deserialised from JSON
    /// missing the value field.
    /// </summary>
    [Fact]
    public void MissingValueError_ReturnsStableErrorShape()
    {
        // Act
        IntegrationError error = ResultJsonShape.MissingValueError();

        // Assert
        error.Code.Should().Be("Serialization.MissingValue");
        error.Type.Should().Be(ErrorType.Failure);
        error.Message.Should().Contain("missing");
    }

    /// <summary>
    /// Verifies that <see cref="ResultJsonShape.MissingErrorsError"/> returns a stable error
    /// shape that converters can use when a failed Result is deserialised from JSON with no
    /// error details.
    /// </summary>
    [Fact]
    public void MissingErrorsError_ReturnsStableErrorShape()
    {
        // Act
        IntegrationError error = ResultJsonShape.MissingErrorsError();

        // Assert
        error.Code.Should().Be("Serialization.MissingErrors");
        error.Type.Should().Be(ErrorType.Failure);
        error.Message.Should().Contain("no error details");
    }

    /// <summary>
    /// Verifies that Project followed by Hydrate is a lossless round-trip for an IntegrationError.
    /// This is the contract both converters rely on.
    /// </summary>
    [Fact]
    public void ProjectThenHydrate_IntegrationError_RoundTripsLosslessly()
    {
        // Arrange
        var original = new IntegrationError("Validation.Required", "Name is required.", ErrorType.Validation);

        // Act
        (string code, string message, ErrorType type) = ResultJsonShape.Project(original);
        IntegrationError reconstructed = ResultJsonShape.Hydrate(code, message, type.ToString());

        // Assert
        reconstructed.Code.Should().Be(original.Code);
        reconstructed.Message.Should().Be(original.Message);
        reconstructed.Type.Should().Be(original.Type);
    }
}
