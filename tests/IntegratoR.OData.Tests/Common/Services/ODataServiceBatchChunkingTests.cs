using FluentResults;
using FluentAssertions;
using IntegratoR.Abstractions.Common.Batch;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.Common.Services;
using IntegratoR.OData.Domain.Models;
using IntegratoR.OData.Interfaces.Services;
using IntegratoR.TestKit.Assertions;
using IntegratoR.TestKit.Builders;
using IntegratoR.TestKit.Doubles.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace IntegratoR.OData.Tests.Common.Services;

/// <summary>
/// Tests <see cref="ODataService{TEntity}"/> batch chunking and <see cref="BatchOutcome"/> aggregation
/// with a substituted adapter: splitting by <c>MaxOperationsPerChunk</c>, global index / chunk-index
/// aggregation, <c>StopOnFirstFailedChunk</c>, and the <see cref="Result{T}"/> success/failure shape.
/// </summary>
public class ODataServiceBatchChunkingTests
{
    private readonly IODataClientAdapter _client = Substitute.For<IODataClientAdapter>();
    private readonly ODataService<TestEntity> _sut;

    public ODataServiceBatchChunkingTests()
        => _sut = new ODataService<TestEntity>(_client, Substitute.For<ILogger<ODataService<TestEntity>>>());

    private static List<TestEntity> Entities(int count) =>
        [.. Enumerable.Range(0, count).Select(i => TestEntityBuilder.Default().WithId($"id-{i}").Build())];

    private static IReadOnlyList<BatchOperationResult> ChunkResults(int count, int? failLocalIndex = null) =>
        [.. Enumerable.Range(0, count).Select(i => new BatchOperationResult
        {
            Index = i,
            StatusCode = i == failLocalIndex ? 400 : 204,
            IsSuccess = i != failLocalIndex,
            ErrorMessage = i == failLocalIndex ? "HTTP 400" : null,
        })];

    private void OnCreate(Func<int, IReadOnlyList<BatchOperationResult>> perChunk) =>
        _client.BatchCreateAsync(Arg.Any<string>(), Arg.Any<IEnumerable<IDictionary<string, object>>>(),
                Arg.Any<BatchFailureMode>(), Arg.Any<CancellationToken>())
            .Returns(ci => perChunk(ci.Arg<IEnumerable<IDictionary<string, object>>>().Count()));

    private Task ReceivedCreateCalls(int times) =>
        _client.Received(times).BatchCreateAsync(Arg.Any<string>(),
            Arg.Any<IEnumerable<IDictionary<string, object>>>(), Arg.Any<BatchFailureMode>(), Arg.Any<CancellationToken>());

    [Fact]
    public async Task AddBatch_SplitsIntoChunks_AndAggregatesGlobalIndices()
    {
        OnCreate(count => ChunkResults(count));

        Result<BatchOutcome> result = await _sut.AddBatchAsync(
            Entities(5), new BatchOptions { MaxOperationsPerChunk = 2 }, CancellationToken.None);

        await ReceivedCreateCalls(3); // 2 + 2 + 1
        result.Should().BeSuccessful();
        BatchOutcome outcome = result.Value;
        outcome.Total.Should().Be(5);
        outcome.ChunkCount.Should().Be(3);
        outcome.AllSucceeded.Should().BeTrue();
        outcome.Items.Select(i => i.Index).Should().Equal(0, 1, 2, 3, 4);
        outcome.Items.Select(i => i.ChunkIndex).Should().Equal(0, 0, 1, 1, 2);
    }

    [Fact]
    public async Task AddBatch_Atomic_StopsAfterFirstFailedChunk()
    {
        int call = 0;
        OnCreate(count =>
        {
            call++;
            return ChunkResults(count, failLocalIndex: call == 1 ? 0 : null); // first chunk fails
        });

        Result<BatchOutcome> result = await _sut.AddBatchAsync(
            Entities(6), new BatchOptions { Mode = BatchFailureMode.Atomic, MaxOperationsPerChunk = 2 }, CancellationToken.None);

        await ReceivedCreateCalls(1); // stopped after the first failed chunk
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.BatchFailed");
        var error = (BatchIntegrationError)result.GetError()!;
        error.Outcome.ChunkCount.Should().Be(1);
        error.Outcome.Total.Should().Be(2); // only the first chunk was submitted
    }

    [Fact]
    public async Task AddBatch_ContinueOnError_PartialFailure_CarriesFailureInOutcome()
    {
        OnCreate(count => ChunkResults(count, failLocalIndex: 1)); // item at local index 1 fails

        Result<BatchOutcome> result = await _sut.AddBatchAsync(
            Entities(3), new BatchOptions { Mode = BatchFailureMode.ContinueOnError }, CancellationToken.None);

        result.Should().BeFailed();
        var error = (BatchIntegrationError)result.GetError()!;
        error.Outcome.Total.Should().Be(3);
        error.Outcome.Failed.Should().Be(1);
        error.Outcome.Failures.Should().ContainSingle(f => f.Index == 1 && f.StatusCode == 400);
    }

    [Fact]
    public async Task AddBatch_AllSucceed_ReturnsOkOutcome()
    {
        OnCreate(count => ChunkResults(count));

        Result<BatchOutcome> result = await _sut.AddBatchAsync(Entities(3), cancellationToken: CancellationToken.None);

        result.Should().BeSuccessful();
        result.Value.AllSucceeded.Should().BeTrue();
        result.Value.Total.Should().Be(3);
    }
}
