using FluentAssertions;
using IntegratoR.Abstractions.Common.Batch;
using Xunit;

namespace IntegratoR.Abstractions.Tests.Common.Batch;

/// <summary>
/// Tests the computed aggregation on <see cref="BatchOutcome"/> (totals and the failure projection).
/// </summary>
public class BatchOutcomeTests
{
    private static BatchItemResult Item(int index, bool ok) => new()
    {
        Index = index,
        ChunkIndex = 0,
        IsSuccess = ok,
        StatusCode = ok ? 204 : 400,
        ErrorCode = ok ? null : "Entity.Failed",
        ErrorMessage = ok ? null : "boom",
    };

    [Fact]
    public void Outcome_WithMixedItems_ComputesTotalsAndFailures()
    {
        var outcome = new BatchOutcome
        {
            Mode = BatchFailureMode.ContinueOnError,
            ChunkCount = 1,
            Items = [Item(0, true), Item(1, false), Item(2, true), Item(3, false)],
        };

        outcome.Total.Should().Be(4);
        outcome.Succeeded.Should().Be(2);
        outcome.Failed.Should().Be(2);
        outcome.AllSucceeded.Should().BeFalse();
        outcome.Failures.Select(f => f.Index).Should().Equal(1, 3);
    }

    [Fact]
    public void Outcome_WithNoFailures_ReportsAllSucceeded()
    {
        var outcome = new BatchOutcome
        {
            Mode = BatchFailureMode.Atomic,
            ChunkCount = 1,
            Items = [Item(0, true), Item(1, true)],
        };

        outcome.AllSucceeded.Should().BeTrue();
        outcome.Failed.Should().Be(0);
        outcome.Failures.Should().BeEmpty();
    }
}
