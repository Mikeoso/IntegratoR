using System.Text.Json;
using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Common.Results.SystemText;
using Microsoft.DurableTask.Converters;
using Xunit;

namespace IntegratoR.Application.Tests.Common.Services;

/// <summary>
/// Integration tests that round-trip <see cref="Result{T}"/> through a real
/// <see cref="JsonDataConverter"/> instance — the same type used by the Durable Functions
/// isolated worker SDK to serialise activity inputs and outputs into the task hub.
/// These tests prove the wiring in <c>SampleFunction/Program.cs</c> works end-to-end:
/// a <c>JsonSerializerOptions</c> configured via <see cref="JsonSerializerOptionsExtensions.AddResultConverters"/>
/// successfully round-trips Result&lt;T&gt; through the Durable Functions code path.
/// </summary>
public sealed class DurableTaskJsonDataConverterResultTests
{
    private sealed record JournalHeaderModel(string DataAreaId, string JournalBatchNumber, string JournalName);

    private static JsonDataConverter CreateConverter()
    {
        JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
        jsonOptions.AddResultConverters();
        return new JsonDataConverter(jsonOptions);
    }

    /// <summary>
    /// Verifies that a successful Result&lt;T&gt; returned from an activity round-trips through
    /// the Durable Task data converter without losing the inner value. This is the exact path
    /// <c>context.CallActivityAsync&lt;Result&lt;T&gt;&gt;</c> takes when the orchestrator deserialises
    /// the activity output from the task hub.
    /// </summary>
    [Fact]
    public void Serialize_Deserialize_SuccessfulResult_PreservesValue()
    {
        // Arrange
        JsonDataConverter converter = CreateConverter();
        var header = new JournalHeaderModel("USMF", "00123", "GenJrn");
        Result<JournalHeaderModel> original = Result.Ok(header);

        // Act
        string? serialized = converter.Serialize(original);
        Result<JournalHeaderModel>? deserialized = converter.Deserialize(
            serialized,
            typeof(Result<JournalHeaderModel>)) as Result<JournalHeaderModel>;

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.IsSuccess.Should().BeTrue();
        deserialized.Value.Should().Be(header);
    }

    /// <summary>
    /// Verifies that a failed Result&lt;T&gt; returned from an activity round-trips with all
    /// IntegrationError metadata intact. This is the regression case — without converters this
    /// throws "JSON value could not be converted to FluentResults.Result..." in production.
    /// </summary>
    [Fact]
    public void Serialize_Deserialize_FailedResult_PreservesIntegrationErrorMetadata()
    {
        // Arrange
        JsonDataConverter converter = CreateConverter();
        var error = new IntegrationError(
            "Journal.CreateFailed",
            "Could not create journal header in F&O.",
            ErrorType.Failure);
        Result<JournalHeaderModel> original = Result.Fail<JournalHeaderModel>(error);

        // Act
        string? serialized = converter.Serialize(original);
        Result<JournalHeaderModel>? deserialized = converter.Deserialize(
            serialized,
            typeof(Result<JournalHeaderModel>)) as Result<JournalHeaderModel>;

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.IsFailed.Should().BeTrue();
        deserialized.Errors.Should().HaveCount(1);

        IntegrationError reconstructed = (IntegrationError)deserialized.Errors[0];
        reconstructed.Code.Should().Be("Journal.CreateFailed");
        reconstructed.Message.Should().Be("Could not create journal header in F&O.");
        reconstructed.Type.Should().Be(ErrorType.Failure);
    }

    /// <summary>
    /// Verifies that a Result&lt;List&lt;T&gt;&gt; — the shape used by the JournalOrchestrators fan-in
    /// pattern — round-trips through the Durable Task data converter.
    /// </summary>
    [Fact]
    public void Serialize_Deserialize_SuccessfulResultWithList_PreservesAllElements()
    {
        // Arrange
        JsonDataConverter converter = CreateConverter();
        var headers = new List<JournalHeaderModel>
        {
            new("USMF", "00123", "GenJrn"),
            new("USMF", "00124", "GenJrn")
        };
        Result<List<JournalHeaderModel>> original = Result.Ok(headers);

        // Act
        string? serialized = converter.Serialize(original);
        Result<List<JournalHeaderModel>>? deserialized = converter.Deserialize(
            serialized,
            typeof(Result<List<JournalHeaderModel>>)) as Result<List<JournalHeaderModel>>;

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.IsSuccess.Should().BeTrue();
        deserialized.Value.Should().BeEquivalentTo(headers);
    }

    /// <summary>
    /// Verifies that a non-generic <see cref="Result"/> (e.g. <c>Result.Ok()</c> from an activity
    /// or sub-orchestrator returning the non-generic Result) round-trips through Durable Task
    /// serialisation. JournalOrchestrators uses this shape via
    /// <c>context.CallActivityAsync&lt;Result&gt;(...)</c> and
    /// <c>context.CallSubOrchestratorAsync&lt;Result&gt;(...)</c>.
    /// </summary>
    [Fact]
    public void Serialize_Deserialize_NonGenericSuccessfulResult_RoundTrips()
    {
        // Arrange
        JsonDataConverter converter = CreateConverter();
        Result original = Result.Ok();

        // Act
        string? serialized = converter.Serialize(original);
        Result? deserialized = converter.Deserialize(serialized, typeof(Result)) as Result;

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that a non-generic failed <see cref="Result"/> with an
    /// <see cref="IntegrationError"/> round-trips through Durable Task serialisation.
    /// </summary>
    [Fact]
    public void Serialize_Deserialize_NonGenericFailedResult_PreservesIntegrationErrorMetadata()
    {
        // Arrange
        JsonDataConverter converter = CreateConverter();
        var error = new IntegrationError(
            "SubOrchestrator.Failed",
            "Sub-orchestrator failed for company 'USMF'.",
            ErrorType.Failure);
        Result original = Result.Fail(error);

        // Act
        string? serialized = converter.Serialize(original);
        Result? deserialized = converter.Deserialize(serialized, typeof(Result)) as Result;

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.IsFailed.Should().BeTrue();
        deserialized.Errors.Should().HaveCount(1);

        IntegrationError reconstructed = (IntegrationError)deserialized.Errors[0];
        reconstructed.Code.Should().Be("SubOrchestrator.Failed");
        reconstructed.Message.Should().Be("Sub-orchestrator failed for company 'USMF'.");
        reconstructed.Type.Should().Be(ErrorType.Failure);
    }
}
