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
    /// Verifies that Project throws on any IError that is not an IntegrationError. IntegrationError
    /// is the only IError implementation in this codebase; a defensive fallback would be dead code.
    /// </summary>
    [Fact]
    public void Project_NonIntegrationError_Throws()
    {
        // Arrange
        var customError = new CustomError("Something failed");

        // Act
        Action act = () => ResultJsonShape.Project(customError);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported IError type*CustomError*");
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
