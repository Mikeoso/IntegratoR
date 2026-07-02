namespace IntegratoR.Abstractions.Common.Batch;

/// <summary>
/// The aggregate result of a batch write across every chunk, listing the per-item outcome so callers
/// can report which records committed and which failed.
/// </summary>
/// <remarks>
/// A batch that failed is returned as a failed <see cref="FluentResults.Result{T}"/> whose error is a
/// <c>BatchIntegrationError</c> carrying this outcome, so the failure list is available even when
/// <see cref="FluentResults.ResultBase.IsSuccess"/> is <c>false</c>.
/// </remarks>
public sealed record BatchOutcome
{
    /// <summary>
    /// Gets the failure mode the batch ran under.
    /// </summary>
    public required BatchFailureMode Mode { get; init; }

    /// <summary>
    /// Gets the number of chunks the input was split into and submitted as separate <c>$batch</c> requests.
    /// </summary>
    public required int ChunkCount { get; init; }

    /// <summary>
    /// Gets the per-item outcome of every submitted operation, in original input order.
    /// </summary>
    public required IReadOnlyList<BatchItemResult> Items { get; init; }

    /// <summary>
    /// Gets the total number of operations submitted.
    /// </summary>
    public int Total => Items.Count;

    /// <summary>
    /// Gets the number of operations that committed successfully.
    /// </summary>
    public int Succeeded => Items.Count(item => item.IsSuccess);

    /// <summary>
    /// Gets the number of operations that failed.
    /// </summary>
    public int Failed => Items.Count(item => !item.IsSuccess);

    /// <summary>
    /// Gets a value indicating whether every operation committed successfully.
    /// </summary>
    public bool AllSucceeded => Failed == 0;

    /// <summary>
    /// Gets the operations that failed, for error analysis and retry.
    /// </summary>
    public IEnumerable<BatchItemResult> Failures => Items.Where(item => !item.IsSuccess);
}
