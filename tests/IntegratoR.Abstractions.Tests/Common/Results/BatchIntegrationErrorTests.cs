using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.Batch;
using IntegratoR.Abstractions.Common.Results;
using Xunit;

namespace IntegratoR.Abstractions.Tests.Common.Results;

/// <summary>
/// Tests that <see cref="BatchIntegrationError"/> carries the <see cref="BatchOutcome"/> and remains
/// retrievable from a failed <see cref="Result{T}"/> via <c>GetError()</c> — the mechanism that keeps
/// the per-item failure list available when a batch fails.
/// </summary>
public class BatchIntegrationErrorTests
{
    private static BatchOutcome FailingOutcome() => new()
    {
        Mode = BatchFailureMode.Atomic,
        ChunkCount = 1,
        Items =
        [
            new BatchItemResult { Index = 0, ChunkIndex = 0, IsSuccess = false, StatusCode = 403, ErrorCode = "Entity.Denied", ErrorMessage = "no" },
        ],
    };

    [Fact]
    public void BatchIntegrationError_CarriesOutcome_AndIsAnIntegrationError()
    {
        BatchOutcome outcome = FailingOutcome();

        var error = new BatchIntegrationError("Entity.BatchFailed", "1 of 1 operations failed", outcome);

        error.Should().BeAssignableTo<IntegrationError>();
        error.Code.Should().Be("Entity.BatchFailed");
        error.Type.Should().Be(ErrorType.Failure);
        error.Message.Should().Be("1 of 1 operations failed");
        error.Outcome.Should().BeSameAs(outcome);
    }

    [Fact]
    public void FailedResult_ExposesOutcome_ViaGetError()
    {
        BatchOutcome outcome = FailingOutcome();
        Result<BatchOutcome> result = Result.Fail<BatchOutcome>(
            new BatchIntegrationError("Entity.BatchFailed", "1 of 1 operations failed", outcome));

        result.IsFailed.Should().BeTrue();
        IntegrationError? error = result.GetError();
        error.Should().BeOfType<BatchIntegrationError>();
        ((BatchIntegrationError)error!).Outcome.Failures.Should().ContainSingle(f => f.ErrorCode == "Entity.Denied");
    }
}
