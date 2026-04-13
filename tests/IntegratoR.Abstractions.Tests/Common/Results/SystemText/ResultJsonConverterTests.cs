using System.Text.Json;
using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Common.Results.SystemText;
using Xunit;

namespace IntegratoR.Abstractions.Tests.Common.Results.SystemText;

/// <summary>
/// Unit tests for the System.Text.Json <see cref="ResultJsonConverter{T}"/> and
/// <see cref="ResultJsonConverterFactory"/>. Covers Result&lt;T&gt; round-tripping for
/// the System.Text.Json code paths used by Durable Functions and the distributed cache.
/// </summary>
public sealed class ResultJsonConverterTests
{
    private sealed record PersonModel(string FirstName, string LastName, int Age);

    private static JsonSerializerOptions BuildOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }.AddResultConverters();
    }

    /// <summary>
    /// Verifies that a successful Result with a primitive value serialises to the documented JSON shape.
    /// The errors array is omitted on success to keep the cached payload small.
    /// </summary>
    [Fact]
    public void Serialize_SuccessWithPrimitiveValue_WritesIsSuccessAndValue()
    {
        // Arrange
        Result<int> result = Result.Ok(42);
        JsonSerializerOptions options = BuildOptions();

        // Act
        string json = JsonSerializer.Serialize(result, options);

        // Assert
        json.Should().Contain("\"isSuccess\":true");
        json.Should().Contain("\"value\":42");
        json.Should().NotContain("\"errors\"");
    }

    /// <summary>
    /// Verifies that a successful Result with a complex value round-trips losslessly.
    /// </summary>
    [Fact]
    public void RoundTrip_SuccessWithComplexValue_PreservesAllFields()
    {
        // Arrange
        var person = new PersonModel("Ada", "Lovelace", 36);
        Result<PersonModel> original = Result.Ok(person);
        JsonSerializerOptions options = BuildOptions();

        // Act
        string json = JsonSerializer.Serialize(original, options);
        Result<PersonModel>? roundTripped = JsonSerializer.Deserialize<Result<PersonModel>>(json, options);

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsSuccess.Should().BeTrue();
        roundTripped.Value.Should().Be(person);
    }

    /// <summary>
    /// Verifies that a failed Result with an IntegrationError round-trips its Code, Message, and Type.
    /// </summary>
    [Fact]
    public void RoundTrip_FailedWithIntegrationError_PreservesCodeMessageAndType()
    {
        // Arrange
        var error = new IntegrationError("OData.NotFound", "Customer 'C001' not found.", ErrorType.NotFound);
        Result<PersonModel> original = Result.Fail<PersonModel>(error);
        JsonSerializerOptions options = BuildOptions();

        // Act
        string json = JsonSerializer.Serialize(original, options);
        Result<PersonModel>? roundTripped = JsonSerializer.Deserialize<Result<PersonModel>>(json, options);

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsFailed.Should().BeTrue();
        roundTripped.Errors.Should().HaveCount(1);

        IError firstError = roundTripped.Errors[0];
        firstError.Should().BeOfType<IntegrationError>();

        IntegrationError reconstructed = (IntegrationError)firstError;
        reconstructed.Code.Should().Be("OData.NotFound");
        reconstructed.Message.Should().Be("Customer 'C001' not found.");
        reconstructed.Type.Should().Be(ErrorType.NotFound);
    }

    /// <summary>
    /// Verifies that nested generics like Result&lt;List&lt;T&gt;&gt; round-trip correctly.
    /// </summary>
    [Fact]
    public void RoundTrip_NestedGenericValue_PreservesCollection()
    {
        // Arrange
        var people = new List<PersonModel>
        {
            new("Ada", "Lovelace", 36),
            new("Alan", "Turing", 41)
        };
        Result<List<PersonModel>> original = Result.Ok(people);
        JsonSerializerOptions options = BuildOptions();

        // Act
        string json = JsonSerializer.Serialize(original, options);
        Result<List<PersonModel>>? roundTripped = JsonSerializer.Deserialize<Result<List<PersonModel>>>(json, options);

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsSuccess.Should().BeTrue();
        roundTripped.Value.Should().BeEquivalentTo(people);
    }

    /// <summary>
    /// Verifies that multiple errors in a failed Result all round-trip.
    /// </summary>
    [Fact]
    public void RoundTrip_FailedWithMultipleErrors_PreservesAll()
    {
        // Arrange
        var errors = new List<IError>
        {
            new IntegrationError("Validation.Required", "Name is required.", ErrorType.Validation),
            new IntegrationError("Validation.TooLong", "Name exceeds 100 characters.", ErrorType.Validation)
        };
        Result<PersonModel> original = Result.Fail<PersonModel>(errors);
        JsonSerializerOptions options = BuildOptions();

        // Act
        string json = JsonSerializer.Serialize(original, options);
        Result<PersonModel>? roundTripped = JsonSerializer.Deserialize<Result<PersonModel>>(json, options);

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.Errors.Should().HaveCount(2);
        roundTripped.Errors.Select(e => ((IntegrationError)e).Code)
            .Should().BeEquivalentTo("Validation.Required", "Validation.TooLong");
    }

    /// <summary>
    /// Verifies that a Result.Ok with a null reference value round-trips and stays null.
    /// </summary>
    [Fact]
    public void RoundTrip_SuccessWithNullValue_PreservesNull()
    {
        // Arrange
        Result<PersonModel?> original = Result.Ok<PersonModel?>(null);
        JsonSerializerOptions options = BuildOptions();

        // Act
        string json = JsonSerializer.Serialize(original, options);
        Result<PersonModel?>? roundTripped = JsonSerializer.Deserialize<Result<PersonModel?>>(json, options);

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsSuccess.Should().BeTrue();
        roundTripped.Value.Should().BeNull();
    }

    /// <summary>
    /// Verifies that the factory accepts any closed Result&lt;T&gt; and rejects unrelated types.
    /// </summary>
    [Fact]
    public void Factory_CanConvert_OnlyMatchesClosedGenericResult()
    {
        // Arrange
        var factory = new ResultJsonConverterFactory();

        // Act + Assert
        factory.CanConvert(typeof(Result<int>)).Should().BeTrue();
        factory.CanConvert(typeof(Result<PersonModel>)).Should().BeTrue();
        factory.CanConvert(typeof(Result<List<int>>)).Should().BeTrue();
        factory.CanConvert(typeof(Result<>)).Should().BeFalse();
        factory.CanConvert(typeof(Result)).Should().BeFalse();
        factory.CanConvert(typeof(int)).Should().BeFalse();
        factory.CanConvert(typeof(PersonModel)).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that AddResultConverters registers both the generic factory and the non-generic
    /// Result converter, and returns the same options instance for fluent chaining.
    /// </summary>
    [Fact]
    public void AddResultConverters_RegistersBothConvertersAndReturnsOptions()
    {
        // Arrange
        var options = new JsonSerializerOptions();

        // Act
        JsonSerializerOptions returned = options.AddResultConverters();

        // Assert
        returned.Should().BeSameAs(options);
        options.Converters.Should().ContainSingle(c => c is ResultJsonConverterFactory);
        options.Converters.Should().ContainSingle(c => c is NonGenericResultJsonConverter);
    }

    /// <summary>
    /// Verifies that AddResultConverters is idempotent — calling it twice on the same options
    /// instance does not register duplicate converters.
    /// </summary>
    [Fact]
    public void AddResultConverters_CalledTwice_DoesNotDuplicate()
    {
        // Arrange
        var options = new JsonSerializerOptions();

        // Act
        options.AddResultConverters();
        options.AddResultConverters();

        // Assert
        options.Converters.OfType<ResultJsonConverterFactory>().Should().ContainSingle();
        options.Converters.OfType<NonGenericResultJsonConverter>().Should().ContainSingle();
    }

    /// <summary>
    /// Verifies that AddResultConverters throws on null options.
    /// </summary>
    [Fact]
    public void AddResultConverters_NullOptions_Throws()
    {
        // Act
        Action act = () => ((JsonSerializerOptions)null!).AddResultConverters();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that the non-generic Result converter round-trips a successful Result.Ok().
    /// This is the regression case for activities returning the non-generic Result through
    /// CallActivityAsync&lt;Result&gt;(...).
    /// </summary>
    [Fact]
    public void NonGenericResult_RoundTrip_SuccessResult()
    {
        // Arrange
        Result original = Result.Ok();
        JsonSerializerOptions options = BuildOptions();

        // Act
        string json = JsonSerializer.Serialize(original, options);
        Result? roundTripped = JsonSerializer.Deserialize<Result>(json, options);

        // Assert
        json.Should().Contain("\"isSuccess\":true");
        json.Should().NotContain("\"errors\"");
        roundTripped.Should().NotBeNull();
        roundTripped!.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that the non-generic Result converter round-trips a failed Result with
    /// IntegrationError metadata intact.
    /// </summary>
    [Fact]
    public void NonGenericResult_RoundTrip_FailedResultWithIntegrationError()
    {
        // Arrange
        var error = new IntegrationError("Activity.Failed", "Activity failed.", ErrorType.Failure);
        Result original = Result.Fail(error);
        JsonSerializerOptions options = BuildOptions();

        // Act
        string json = JsonSerializer.Serialize(original, options);
        Result? roundTripped = JsonSerializer.Deserialize<Result>(json, options);

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsFailed.Should().BeTrue();
        IntegrationError reconstructed = (IntegrationError)roundTripped.Errors[0];
        reconstructed.Code.Should().Be("Activity.Failed");
        reconstructed.Message.Should().Be("Activity failed.");
        reconstructed.Type.Should().Be(ErrorType.Failure);
    }

    /// <summary>
    /// Verifies that the non-generic Result converter round-trips a failed Result with a plain
    /// (non-IntegrationError) error using the lenient fallback shape — this is the contract
    /// the Newtonsoft converters have always honoured and that ResultJsonShape.Project now matches.
    /// </summary>
    [Fact]
    public void NonGenericResult_RoundTrip_FailedResultWithPlainError()
    {
        // Arrange
        Result original = Result.Fail("something went wrong");
        JsonSerializerOptions options = BuildOptions();

        // Act
        string json = JsonSerializer.Serialize(original, options);
        Result? roundTripped = JsonSerializer.Deserialize<Result>(json, options);

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsFailed.Should().BeTrue();
        IntegrationError reconstructed = (IntegrationError)roundTripped.Errors[0];
        reconstructed.Code.Should().Be("Unknown");
        reconstructed.Message.Should().Be("something went wrong");
        reconstructed.Type.Should().Be(ErrorType.Failure);
    }

    /// <summary>
    /// Verifies that deserialising a corrupted "successful" payload that is missing the required
    /// <c>value</c> field produces a failed Result with the synthetic Serialization.MissingValue
    /// error rather than silently returning Result.Ok(default(T)). This is the regression coverage
    /// for the silent-default(T) issue flagged in code review.
    /// </summary>
    [Fact]
    public void Read_SuccessJsonMissingValueField_ReturnsFailedResultWithSyntheticError()
    {
        // Arrange
        JsonSerializerOptions options = BuildOptions();
        string corruptedJson = "{\"isSuccess\":true}";

        // Act
        Result<int>? result = JsonSerializer.Deserialize<Result<int>>(corruptedJson, options);

        // Assert
        result.Should().NotBeNull();
        result!.IsFailed.Should().BeTrue();
        IntegrationError error = (IntegrationError)result.Errors[0];
        error.Code.Should().Be("Serialization.MissingValue");
    }

    /// <summary>
    /// Verifies that deserialising a "successful" payload with an explicit null value field
    /// (which is legitimate for nullable T) is still handled correctly and not confused with
    /// the missing-value corruption case.
    /// </summary>
    [Fact]
    public void Read_SuccessJsonWithExplicitNullValue_ReturnsSuccessfulResultWithNull()
    {
        // Arrange
        JsonSerializerOptions options = BuildOptions();
        string explicitNullJson = "{\"isSuccess\":true,\"value\":null}";

        // Act
        Result<PersonModel?>? result = JsonSerializer.Deserialize<Result<PersonModel?>>(explicitNullJson, options);

        // Assert
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    /// <summary>
    /// Verifies that deserialising a corrupted "failed" payload with an empty errors array
    /// produces a failed Result with the synthetic Serialization.MissingErrors error rather
    /// than a failed Result with no error details.
    /// </summary>
    [Fact]
    public void Read_FailedJsonWithEmptyErrors_ReturnsFailedResultWithSyntheticError()
    {
        // Arrange
        JsonSerializerOptions options = BuildOptions();
        string corruptedJson = "{\"isSuccess\":false,\"errors\":[]}";

        // Act
        Result<int>? result = JsonSerializer.Deserialize<Result<int>>(corruptedJson, options);

        // Assert
        result.Should().NotBeNull();
        result!.IsFailed.Should().BeTrue();
        IntegrationError error = (IntegrationError)result.Errors[0];
        error.Code.Should().Be("Serialization.MissingErrors");
    }

    /// <summary>
    /// Verifies the same missing-errors handling for the non-generic <see cref="Result"/>.
    /// </summary>
    [Fact]
    public void NonGenericResult_Read_FailedJsonWithEmptyErrors_ReturnsFailedResultWithSyntheticError()
    {
        // Arrange
        JsonSerializerOptions options = BuildOptions();
        string corruptedJson = "{\"isSuccess\":false}";

        // Act
        Result? result = JsonSerializer.Deserialize<Result>(corruptedJson, options);

        // Assert
        result.Should().NotBeNull();
        result!.IsFailed.Should().BeTrue();
        IntegrationError error = (IntegrationError)result.Errors[0];
        error.Code.Should().Be("Serialization.MissingErrors");
    }

    /// <summary>
    /// Verifies that a JSON literal <c>null</c> payload deserialises to a null Result&lt;T&gt;
    /// rather than throwing. Symmetric with Write which emits null for a null Result&lt;T&gt;.
    /// </summary>
    [Fact]
    public void Read_NullJsonPayload_ReturnsNull()
    {
        // Arrange
        JsonSerializerOptions options = BuildOptions();

        // Act
        Result<int>? result = JsonSerializer.Deserialize<Result<int>>("null", options);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that an explicit <c>"value": null</c> on a non-nullable value type (e.g.
    /// Result&lt;int&gt;) is treated as corruption and returns a failed Result with the
    /// MissingValue synthetic error — rather than silently returning Result.Ok(0).
    /// Symmetric with the missing-value-field handling.
    /// </summary>
    [Fact]
    public void Read_SuccessJsonExplicitNullValueOnNonNullableValueType_ReturnsFailedResult()
    {
        // Arrange
        JsonSerializerOptions options = BuildOptions();
        string corruptedJson = "{\"isSuccess\":true,\"value\":null}";

        // Act
        Result<int>? result = JsonSerializer.Deserialize<Result<int>>(corruptedJson, options);

        // Assert
        result.Should().NotBeNull();
        result!.IsFailed.Should().BeTrue();
        IntegrationError error = (IntegrationError)result.Errors[0];
        error.Code.Should().Be("Serialization.MissingValue");
    }

    /// <summary>
    /// Verifies that an explicit <c>"value": null</c> on a nullable value type
    /// (e.g. Result&lt;int?&gt;) is still legitimate and round-trips as Result.Ok(null).
    /// </summary>
    [Fact]
    public void Read_SuccessJsonExplicitNullValueOnNullableValueType_ReturnsSuccessfulNullResult()
    {
        // Arrange
        JsonSerializerOptions options = BuildOptions();
        string json = "{\"isSuccess\":true,\"value\":null}";

        // Act
        Result<int?>? result = JsonSerializer.Deserialize<Result<int?>>(json, options);

        // Assert
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }
}
