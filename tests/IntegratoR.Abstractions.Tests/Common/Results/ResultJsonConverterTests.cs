using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using Newtonsoft.Json;
using Xunit;

namespace IntegratoR.Abstractions.Tests.Common.Results;

/// <summary>
/// Unit tests for <see cref="ResultJsonConverter"/> covering serialization and deserialization
/// of non-generic <see cref="Result"/> objects.
/// </summary>
public sealed class ResultJsonConverterTests
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Converters = [new ResultJsonConverter()]
    };

    /// <summary>
    /// Verifies that a successful result is serialized with <c>isSuccess=true</c> and the
    /// errors array is omitted entirely (saves bytes on the cache/Durable activity hot path).
    /// </summary>
    [Fact]
    public void WriteJson_SuccessResult_WritesIsSuccessTrueAndOmitsErrors()
    {
        // Arrange
        var result = Result.Ok();

        // Act
        var json = JsonConvert.SerializeObject(result, Settings);

        // Assert
        json.Should().Contain("\"isSuccess\":true");
        json.Should().NotContain("\"errors\"");
    }

    /// <summary>
    /// Verifies that a failed result with an <see cref="IntegrationError"/> serializes
    /// the error code, message, and type correctly.
    /// </summary>
    [Fact]
    public void WriteJson_FailedWithIntegrationError_WritesCodeMessageType()
    {
        // Arrange
        var error = new IntegrationError("ERR404", "Not found", ErrorType.NotFound);
        var result = Result.Fail(error);

        // Act
        var json = JsonConvert.SerializeObject(result, Settings);

        // Assert
        json.Should().Contain("\"isSuccess\":false");
        json.Should().Contain("\"code\":\"ERR404\"");
        json.Should().Contain("\"message\":\"Not found\"");
        json.Should().Contain("\"type\":\"NotFound\"");
    }

    /// <summary>
    /// Verifies that the converter throws on non-<see cref="IntegrationError"/> errors. Per
    /// project convention <see cref="IntegrationError"/> is the only <see cref="IError"/>
    /// implementation; a defensive fallback would be dead code, so the converter delegates
    /// to <see cref="ResultJsonShape.Project"/> which throws to make any future violation loud.
    /// </summary>
    [Fact]
    public void WriteJson_FailedWithPlainError_Throws()
    {
        // Arrange
        var result = Result.Fail("something went wrong");

        // Act
        Action act = () => JsonConvert.SerializeObject(result, Settings);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported IError type*");
    }

    /// <summary>
    /// Verifies that a JSON string representing a successful result deserializes to <c>Result.Ok()</c>.
    /// </summary>
    [Fact]
    public void ReadJson_SuccessJson_ReturnsOkResult()
    {
        // Arrange
        var json = "{\"isSuccess\":true,\"errors\":[]}";

        // Act
        var result = JsonConvert.DeserializeObject<Result>(json, Settings);

        // Assert
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that a JSON string representing a failed result deserializes to a result
    /// containing the expected <see cref="IntegrationError"/> entries.
    /// </summary>
    [Fact]
    public void ReadJson_FailedJson_ReturnsResultWithIntegrationErrors()
    {
        // Arrange
        var json = "{\"isSuccess\":false,\"errors\":[{\"code\":\"VAL001\",\"message\":\"Required field missing\",\"type\":\"Validation\"}]}";

        // Act
        var result = JsonConvert.DeserializeObject<Result>(json, Settings);

        // Assert
        result.Should().NotBeNull();
        result!.IsFailed.Should().BeTrue();
        var integrationError = result.Errors.OfType<IntegrationError>().FirstOrDefault();
        integrationError.Should().NotBeNull();
        integrationError!.Code.Should().Be("VAL001");
        integrationError.Message.Should().Be("Required field missing");
        integrationError.Type.Should().Be(ErrorType.Validation);
    }

    /// <summary>
    /// Verifies that serializing and then deserializing a successful <see cref="Result"/>
    /// preserves the success state.
    /// </summary>
    [Fact]
    public void RoundTrip_SuccessResult_PreservesProperties()
    {
        // Arrange
        var original = Result.Ok();

        // Act
        var json = JsonConvert.SerializeObject(original, Settings);
        var restored = JsonConvert.DeserializeObject<Result>(json, Settings);

        // Assert
        restored.Should().NotBeNull();
        restored!.IsSuccess.Should().BeTrue();
        restored.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that serializing and then deserializing a failed <see cref="Result"/>
    /// preserves the error code and message.
    /// </summary>
    [Fact]
    public void RoundTrip_FailedResult_PreservesProperties()
    {
        // Arrange
        var original = Result.Fail(new IntegrationError("ROUND_TRIP", "Round-trip error", ErrorType.Conflict));

        // Act
        var json = JsonConvert.SerializeObject(original, Settings);
        var restored = JsonConvert.DeserializeObject<Result>(json, Settings);

        // Assert
        restored.Should().NotBeNull();
        restored!.IsFailed.Should().BeTrue();
        var error = restored.Errors.OfType<IntegrationError>().FirstOrDefault();
        error.Should().NotBeNull();
        error!.Code.Should().Be("ROUND_TRIP");
        error.Message.Should().Be("Round-trip error");
        error.Type.Should().Be(ErrorType.Conflict);
    }
}
