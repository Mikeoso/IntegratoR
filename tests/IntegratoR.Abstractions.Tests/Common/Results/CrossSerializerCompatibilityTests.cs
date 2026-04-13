using System.Text.Json;
using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Common.Results.SystemText;
using Newtonsoft.Json;
using Xunit;

namespace IntegratoR.Abstractions.Tests.Common.Results;

/// <summary>
/// Verifies that JSON produced by the Newtonsoft converters can be read by the System.Text.Json
/// converters and vice versa. Both converter families delegate to <see cref="ResultJsonShape"/>
/// for property names and the IError ↔ primitives mapping, so they MUST produce a compatible
/// wire format. These tests pin that contract — without them the shared shape design is just
/// an aspiration.
/// </summary>
public sealed class CrossSerializerCompatibilityTests
{
    private sealed record PersonModel(string FirstName, string LastName, int Age);

    private static readonly Newtonsoft.Json.JsonSerializerSettings NewtonsoftSettings = new()
    {
        Converters =
        {
            new ResultJsonConverter(),
            new ResultGenericJsonConverter()
        }
    };

    private static System.Text.Json.JsonSerializerOptions StjOptions()
    {
        return new System.Text.Json.JsonSerializerOptions().AddResultConverters();
    }

    /// <summary>
    /// Newtonsoft serialises a successful Result&lt;T&gt;, STJ deserialises it, value preserved.
    /// </summary>
    [Fact]
    public void Newtonsoft_Write_Stj_Read_SuccessfulResult_PreservesValue()
    {
        // Arrange
        var person = new PersonModel("Ada", "Lovelace", 36);
        Result<PersonModel> original = Result.Ok(person);

        // Act
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(original, NewtonsoftSettings);
        Result<PersonModel>? roundTripped = System.Text.Json.JsonSerializer.Deserialize<Result<PersonModel>>(json, StjOptions());

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsSuccess.Should().BeTrue();
        roundTripped.Value.Should().Be(person);
    }

    /// <summary>
    /// STJ serialises a successful Result&lt;T&gt;, Newtonsoft deserialises it, value preserved.
    /// </summary>
    [Fact]
    public void Stj_Write_Newtonsoft_Read_SuccessfulResult_PreservesValue()
    {
        // Arrange
        var person = new PersonModel("Ada", "Lovelace", 36);
        Result<PersonModel> original = Result.Ok(person);

        // Act
        string json = System.Text.Json.JsonSerializer.Serialize(original, StjOptions());
        Result<PersonModel>? roundTripped = Newtonsoft.Json.JsonConvert.DeserializeObject<Result<PersonModel>>(json, NewtonsoftSettings);

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsSuccess.Should().BeTrue();
        roundTripped.Value.Should().Be(person);
    }

    /// <summary>
    /// Newtonsoft serialises a failed Result, STJ deserialises it, IntegrationError fields preserved.
    /// </summary>
    [Fact]
    public void Newtonsoft_Write_Stj_Read_FailedResult_PreservesIntegrationError()
    {
        // Arrange
        var error = new IntegrationError("OData.NotFound", "Customer 'C001' not found.", ErrorType.NotFound);
        Result<PersonModel> original = Result.Fail<PersonModel>(error);

        // Act
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(original, NewtonsoftSettings);
        Result<PersonModel>? roundTripped = System.Text.Json.JsonSerializer.Deserialize<Result<PersonModel>>(json, StjOptions());

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsFailed.Should().BeTrue();
        IntegrationError reconstructed = (IntegrationError)roundTripped.Errors[0];
        reconstructed.Code.Should().Be("OData.NotFound");
        reconstructed.Message.Should().Be("Customer 'C001' not found.");
        reconstructed.Type.Should().Be(ErrorType.NotFound);
    }

    /// <summary>
    /// STJ serialises a failed Result, Newtonsoft deserialises it, IntegrationError fields preserved.
    /// </summary>
    [Fact]
    public void Stj_Write_Newtonsoft_Read_FailedResult_PreservesIntegrationError()
    {
        // Arrange
        var error = new IntegrationError("OData.NotFound", "Customer 'C001' not found.", ErrorType.NotFound);
        Result<PersonModel> original = Result.Fail<PersonModel>(error);

        // Act
        string json = System.Text.Json.JsonSerializer.Serialize(original, StjOptions());
        Result<PersonModel>? roundTripped = Newtonsoft.Json.JsonConvert.DeserializeObject<Result<PersonModel>>(json, NewtonsoftSettings);

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsFailed.Should().BeTrue();
        IntegrationError reconstructed = (IntegrationError)roundTripped.Errors[0];
        reconstructed.Code.Should().Be("OData.NotFound");
        reconstructed.Message.Should().Be("Customer 'C001' not found.");
        reconstructed.Type.Should().Be(ErrorType.NotFound);
    }

    /// <summary>
    /// Non-generic <see cref="Result"/>: Newtonsoft writes, STJ reads. Used by JournalOrchestrators
    /// for sub-orchestrator return values.
    /// </summary>
    [Fact]
    public void Newtonsoft_Write_Stj_Read_NonGenericResult_RoundTrips()
    {
        // Arrange
        Result original = Result.Fail(new IntegrationError("X", "msg", ErrorType.Validation));

        // Act
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(original, NewtonsoftSettings);
        Result? roundTripped = System.Text.Json.JsonSerializer.Deserialize<Result>(json, StjOptions());

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsFailed.Should().BeTrue();
        ((IntegrationError)roundTripped.Errors[0]).Code.Should().Be("X");
    }

    /// <summary>
    /// Non-generic <see cref="Result"/>: STJ writes, Newtonsoft reads.
    /// </summary>
    [Fact]
    public void Stj_Write_Newtonsoft_Read_NonGenericResult_RoundTrips()
    {
        // Arrange
        Result original = Result.Fail(new IntegrationError("X", "msg", ErrorType.Validation));

        // Act
        string json = System.Text.Json.JsonSerializer.Serialize(original, StjOptions());
        Result? roundTripped = Newtonsoft.Json.JsonConvert.DeserializeObject<Result>(json, NewtonsoftSettings);

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsFailed.Should().BeTrue();
        ((IntegrationError)roundTripped.Errors[0]).Code.Should().Be("X");
    }

    /// <summary>
    /// Non-generic successful <see cref="Result"/>: Newtonsoft writes, STJ reads. Pins the
    /// success-path cross-serialiser contract that was previously only implicitly covered.
    /// </summary>
    [Fact]
    public void Newtonsoft_Write_Stj_Read_NonGenericSuccessResult_RoundTrips()
    {
        // Arrange
        Result original = Result.Ok();

        // Act
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(original, NewtonsoftSettings);
        Result? roundTripped = System.Text.Json.JsonSerializer.Deserialize<Result>(json, StjOptions());

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Non-generic successful <see cref="Result"/>: STJ writes, Newtonsoft reads.
    /// </summary>
    [Fact]
    public void Stj_Write_Newtonsoft_Read_NonGenericSuccessResult_RoundTrips()
    {
        // Arrange
        Result original = Result.Ok();

        // Act
        string json = System.Text.Json.JsonSerializer.Serialize(original, StjOptions());
        Result? roundTripped = Newtonsoft.Json.JsonConvert.DeserializeObject<Result>(json, NewtonsoftSettings);

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsSuccess.Should().BeTrue();
    }
}
