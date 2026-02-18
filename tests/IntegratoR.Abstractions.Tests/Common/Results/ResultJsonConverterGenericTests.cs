using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using Newtonsoft.Json;
using Xunit;

namespace IntegratoR.Abstractions.Tests.Common.Results;

/// <summary>
/// Unit tests for the generic <see cref="ResultJsonConverter{T}"/> covering serialization
/// and deserialization of <see cref="Result{T}"/> objects.
/// </summary>
public sealed class ResultJsonConverterGenericTests
{
    /// <summary>A simple model used for round-trip serialization tests.</summary>
    private sealed record PersonModel(string FirstName, string LastName, int Age);

    private static JsonSerializerSettings BuildSettings<T>() => new()
    {
        Converters = [new ResultJsonConverter<T>()]
    };

    /// <summary>
    /// Verifies that a successful result is serialized with <c>isSuccess=true</c>, the inner value, and an empty errors array.
    /// </summary>
    [Fact]
    public void WriteJson_SuccessWithValue_WritesIsSuccessValueAndEmptyErrors()
    {
        // Arrange
        var result = Result.Ok(99);
        var settings = BuildSettings<int>();

        // Act
        var json = JsonConvert.SerializeObject(result, settings);

        // Assert
        json.Should().Contain("\"isSuccess\":true");
        json.Should().Contain("\"value\":99");
        json.Should().Contain("\"errors\":[]");
    }

    /// <summary>
    /// Verifies that a failed result is serialized with <c>isSuccess=false</c>, the error details, and no value.
    /// </summary>
    [Fact]
    public void WriteJson_FailedResult_WritesIsSuccessFalseWithErrors()
    {
        // Arrange
        var error = new IntegrationError("ERR500", "Internal error", ErrorType.Failure);
        var result = Result.Fail<int>(error);
        var settings = BuildSettings<int>();

        // Act
        var json = JsonConvert.SerializeObject(result, settings);

        // Assert
        json.Should().Contain("\"isSuccess\":false");
        json.Should().NotContain("\"value\"");
        json.Should().Contain("\"code\":\"ERR500\"");
    }

    /// <summary>
    /// Verifies that deserializing a successful JSON payload returns an <c>Ok</c> result with the inner value.
    /// </summary>
    [Fact]
    public void ReadJson_SuccessJsonWithValue_ReturnsOkResultWithValue()
    {
        // Arrange
        var json = "{\"isSuccess\":true,\"value\":\"hello\",\"errors\":[]}";
        var settings = BuildSettings<string>();

        // Act
        var result = JsonConvert.DeserializeObject<Result<string>>(json, settings);

        // Assert
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    /// <summary>
    /// Verifies that deserializing a failed JSON payload returns a failed result with the expected errors.
    /// </summary>
    [Fact]
    public void ReadJson_FailedJson_ReturnsFailResult()
    {
        // Arrange
        var json = "{\"isSuccess\":false,\"errors\":[{\"code\":\"NF001\",\"message\":\"Entity not found\",\"type\":\"NotFound\"}]}";
        var settings = BuildSettings<string>();

        // Act
        var result = JsonConvert.DeserializeObject<Result<string>>(json, settings);

        // Assert
        result.Should().NotBeNull();
        result!.IsFailed.Should().BeTrue();
        var error = result.Errors.OfType<IntegrationError>().FirstOrDefault();
        error.Should().NotBeNull();
        error!.Code.Should().Be("NF001");
        error.Type.Should().Be(ErrorType.NotFound);
    }

    /// <summary>
    /// Verifies that a successful result containing a complex type round-trips correctly.
    /// </summary>
    [Fact]
    public void RoundTrip_SuccessWithComplexType_PreservesValue()
    {
        // Arrange
        var person = new PersonModel("Jane", "Doe", 30);
        var original = Result.Ok(person);
        var settings = BuildSettings<PersonModel>();

        // Act
        var json = JsonConvert.SerializeObject(original, settings);
        var restored = JsonConvert.DeserializeObject<Result<PersonModel>>(json, settings);

        // Assert
        restored.Should().NotBeNull();
        restored!.IsSuccess.Should().BeTrue();
        restored.Value.FirstName.Should().Be("Jane");
        restored.Value.LastName.Should().Be("Doe");
        restored.Value.Age.Should().Be(30);
    }

    /// <summary>
    /// Verifies that a failed generic result round-trips correctly, preserving the error details.
    /// </summary>
    [Fact]
    public void RoundTrip_FailedResult_PreservesErrors()
    {
        // Arrange
        var original = Result.Fail<string>(new IntegrationError("CODE", "A message", ErrorType.Validation));
        var settings = BuildSettings<string>();

        // Act
        var json = JsonConvert.SerializeObject(original, settings);
        var restored = JsonConvert.DeserializeObject<Result<string>>(json, settings);

        // Assert
        restored.Should().NotBeNull();
        restored!.IsFailed.Should().BeTrue();
        var error = restored.Errors.OfType<IntegrationError>().FirstOrDefault();
        error.Should().NotBeNull();
        error!.Code.Should().Be("CODE");
        error.Message.Should().Be("A message");
        error.Type.Should().Be(ErrorType.Validation);
    }
}
