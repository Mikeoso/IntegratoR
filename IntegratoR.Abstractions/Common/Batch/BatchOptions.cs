namespace IntegratoR.Abstractions.Common.Batch;

/// <summary>
/// Per-call overrides for a single batch write. A <c>null</c> member falls back to the configured
/// <c>ODataSettings.Batch</c> default.
/// </summary>
public sealed record BatchOptions
{
    /// <summary>
    /// Gets the failure mode for this call, or <c>null</c> to use the configured default.
    /// </summary>
    public BatchFailureMode? Mode { get; init; }

    /// <summary>
    /// Gets the maximum number of operations per chunk for this call, or <c>null</c> to use the
    /// configured default. Must be between 1 and 5000 (D365's documented maximum per <c>$batch</c>).
    /// </summary>
    public int? MaxOperationsPerChunk { get; init; }
}
