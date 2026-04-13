using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using Newtonsoft.Json;
using Xunit;

namespace IntegratoR.Abstractions.Tests.Common.Results;

/// <summary>
/// Unit tests for <see cref="ResultGenericJsonConverter"/> -- the dynamic converter that handles
/// any <see cref="Result{T}"/> via reflection without being tied to a specific type parameter.
/// </summary>
public sealed class ResultGenericJsonConverterTests
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Converters = [new ResultGenericJsonConverter()]
    };

    /// <summary>
    /// Verifies that <c>CanConvert</c> returns true for a closed generic <c>Result&lt;string&gt;</c> type.
    /// </summary>
    [Fact]
    public void CanConvert_ResultOfString_ReturnsTrue()
    {
        // Arrange
        var converter = new ResultGenericJsonConverter();

        // Act
        var canConvert = converter.CanConvert(typeof(Result<string>));

        // Assert
        canConvert.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that <c>CanConvert</c> returns false for the non-generic <see cref="Result"/> type.
    /// </summary>
    [Fact]
    public void CanConvert_NonGenericResult_ReturnsFalse()
    {
        // Arrange
        var converter = new ResultGenericJsonConverter();

        // Act
        var canConvert = converter.CanConvert(typeof(Result));

        // Assert
        canConvert.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that <c>CanConvert</c> returns false for an unrelated type.
    /// </summary>
    [Fact]
    public void CanConvert_UnrelatedType_ReturnsFalse()
    {
        // Arrange
        var converter = new ResultGenericJsonConverter();

        // Act
        var canConvert = converter.CanConvert(typeof(string));

        // Assert
        canConvert.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a <c>Result&lt;string&gt;</c> with a value round-trips correctly.
    /// </summary>
    [Fact]
    public void RoundTrip_ResultOfString_PreservesValue()
    {
        // Arrange
        var original = Result.Ok("hello world");

        // Act
        var json = JsonConvert.SerializeObject(original, Settings);
        var restored = JsonConvert.DeserializeObject<Result<string>>(json, Settings);

        // Assert
        restored.Should().NotBeNull();
        restored!.IsSuccess.Should().BeTrue();
        restored.Value.Should().Be("hello world");
    }

    /// <summary>
    /// Verifies that a <c>Result&lt;int&gt;</c> with a value round-trips correctly.
    /// </summary>
    [Fact]
    public void RoundTrip_ResultOfInt_PreservesValue()
    {
        // Arrange
        var original = Result.Ok(12345);

        // Act
        var json = JsonConvert.SerializeObject(original, Settings);
        var restored = JsonConvert.DeserializeObject<Result<int>>(json, Settings);

        // Assert
        restored.Should().NotBeNull();
        restored!.IsSuccess.Should().BeTrue();
        restored.Value.Should().Be(12345);
    }

    /// <summary>
    /// Verifies that a <c>Result&lt;T&gt;</c> containing a complex object round-trips correctly.
    /// </summary>
    [Fact]
    public void RoundTrip_ResultOfComplexObject_PreservesValue()
    {
        // Arrange
        var original = Result.Ok(new { Name = "Alpha", Count = 7 });

        // Act -- serialize the anonymous type via the generic converter
        var json = JsonConvert.SerializeObject(original, Settings);

        // Deserialize into a dynamic to verify structure since anonymous types cannot be easily round-tripped
        dynamic? deserialized = JsonConvert.DeserializeObject<dynamic>(json);

        // Assert
        ((bool)deserialized!.isSuccess).Should().BeTrue();
        ((string)deserialized.value.Name).Should().Be("Alpha");
        ((int)deserialized.value.Count).Should().Be(7);
    }

    /// <summary>
    /// Verifies that a failed <c>Result&lt;string&gt;</c> round-trips correctly, preserving errors.
    /// </summary>
    [Fact]
    public void RoundTrip_FailedResult_PreservesErrors()
    {
        // Arrange
        var original = Result.Fail<string>(new IntegrationError("GEN001", "Generic failure", ErrorType.Failure));

        // Act
        var json = JsonConvert.SerializeObject(original, Settings);
        var restored = JsonConvert.DeserializeObject<Result<string>>(json, Settings);

        // Assert
        restored.Should().NotBeNull();
        restored!.IsFailed.Should().BeTrue();
        var error = restored.Errors.OfType<IntegrationError>().FirstOrDefault();
        error.Should().NotBeNull();
        error!.Code.Should().Be("GEN001");
        error.Message.Should().Be("Generic failure");
        error.Type.Should().Be(ErrorType.Failure);
    }

    /// <summary>
    /// Verifies that the JSON literal <c>null</c> deserialises to a null Result&lt;T&gt; rather
    /// than throwing on JObject.Load.
    /// </summary>
    [Fact]
    public void ReadJson_NullJsonPayload_ReturnsNull()
    {
        // Act
        var result = JsonConvert.DeserializeObject<Result<int>>("null", Settings);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that an explicit <c>"value": null</c> on a non-nullable value type is treated
    /// as corruption and returns Result.Fail with the MissingValue synthetic error rather than
    /// crashing inside the reflection-based Result.Ok&lt;int&gt;(null) call.
    /// </summary>
    [Fact]
    public void ReadJson_SuccessJsonExplicitNullValueOnNonNullableValueType_ReturnsFailedResult()
    {
        // Arrange
        var json = "{\"isSuccess\":true,\"value\":null}";

        // Act
        var result = JsonConvert.DeserializeObject<Result<int>>(json, Settings);

        // Assert
        result.Should().NotBeNull();
        result!.IsFailed.Should().BeTrue();
        var error = result.Errors.OfType<IntegrationError>().FirstOrDefault();
        error.Should().NotBeNull();
        error!.Code.Should().Be("Serialization.MissingValue");
    }
}
