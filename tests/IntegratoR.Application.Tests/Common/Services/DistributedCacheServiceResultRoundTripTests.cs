using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Application.Common.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace IntegratoR.Application.Tests.Common.Services;

/// <summary>
/// Integration tests that exercise <see cref="DistributedCacheService"/> against a real
/// <see cref="MemoryDistributedCache"/> to prove that <see cref="Result{T}"/> values can be
/// serialised, stored, retrieved, and deserialised end-to-end. These tests guard against the
/// regression where Result&lt;T&gt; could not be round-tripped through System.Text.Json.
/// </summary>
public sealed class DistributedCacheServiceResultRoundTripTests
{
    private sealed record CachePayload(string Code, string Description, decimal Amount);

    private static DistributedCacheService CreateSut()
    {
        IDistributedCache cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));
        return new DistributedCacheService(cache);
    }

    /// <summary>
    /// Verifies that a successful <see cref="Result{T}"/> with a complex value round-trips through
    /// the distributed cache without losing field data.
    /// </summary>
    [Fact]
    public async Task SetAsync_GetAsync_SuccessfulResultWithComplexValue_RoundTripsLosslessly()
    {
        // Arrange
        DistributedCacheService sut = CreateSut();
        var entity = new CachePayload("ENT-001", "Test entity", 1234.56m);
        Result<CachePayload> original = Result.Ok(entity);

        // Act
        await sut.SetAsync("key", original);
        Result<CachePayload>? roundTripped = await sut.GetAsync<Result<CachePayload>>("key");

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsSuccess.Should().BeTrue();
        roundTripped.Value.Should().Be(entity);
    }

    /// <summary>
    /// Verifies that a failed <see cref="Result{T}"/> with an <see cref="IntegrationError"/> round-trips
    /// the error code, message, and type. This is the exact regression that originally caused
    /// "JSON value could not be converted to FluentResults.Result..." in production.
    /// </summary>
    [Fact]
    public async Task SetAsync_GetAsync_FailedResultWithIntegrationError_RoundTripsErrorMetadata()
    {
        // Arrange
        DistributedCacheService sut = CreateSut();
        var error = new IntegrationError(
            "OData.NotFound",
            "Customer 'C001' not found.",
            ErrorType.NotFound);
        Result<CachePayload> original = Result.Fail<CachePayload>(error);

        // Act
        await sut.SetAsync("key", original);
        Result<CachePayload>? roundTripped = await sut.GetAsync<Result<CachePayload>>("key");

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsFailed.Should().BeTrue();
        roundTripped.Errors.Should().HaveCount(1);

        IntegrationError reconstructed = (IntegrationError)roundTripped.Errors[0];
        reconstructed.Code.Should().Be("OData.NotFound");
        reconstructed.Message.Should().Be("Customer 'C001' not found.");
        reconstructed.Type.Should().Be(ErrorType.NotFound);
    }

    /// <summary>
    /// Verifies that a Result wrapping a list of entities round-trips fully, including all elements.
    /// </summary>
    [Fact]
    public async Task SetAsync_GetAsync_SuccessfulResultWithCollection_RoundTripsAllElements()
    {
        // Arrange
        DistributedCacheService sut = CreateSut();
        var entities = new List<CachePayload>
        {
            new("ENT-001", "First", 100m),
            new("ENT-002", "Second", 200m),
            new("ENT-003", "Third", 300m)
        };
        Result<List<CachePayload>> original = Result.Ok(entities);

        // Act
        await sut.SetAsync("key", original);
        Result<List<CachePayload>>? roundTripped = await sut.GetAsync<Result<List<CachePayload>>>("key");

        // Assert
        roundTripped.Should().NotBeNull();
        roundTripped!.IsSuccess.Should().BeTrue();
        roundTripped.Value.Should().BeEquivalentTo(entities);
    }
}
