using IntegratoR.Abstractions.Common.Batch;

namespace IntegratoR.OData.Domain.Settings;

/// <summary>
/// Configures how the OData client submits batch writes: the default failure mode, automatic
/// chunking, and how a failed chunk affects the rest of the dataset.
/// </summary>
public class ODataBatchSettings
{
    /// <summary>
    /// Gets or sets the default failure mode for batch writes when a call does not override it.
    /// </summary>
    /// <value>The default value is <see cref="BatchFailureMode.Atomic"/>.</value>
    public BatchFailureMode FailureMode { get; set; } = BatchFailureMode.Atomic;

    /// <summary>
    /// Gets or sets the maximum number of operations sent in a single <c>$batch</c> request; larger
    /// datasets are split into consecutive chunks.
    /// </summary>
    /// <remarks>
    /// D365 F&amp;O enforces a hard maximum of 200 operations per <c>$batch</c> (verified against the
    /// live environment; Microsoft's "service protection API limits" doc cites 5000, but the operational
    /// limit is 200). The default of 150 stays just under that ceiling with headroom.
    /// </remarks>
    /// <value>The default value is <c>150</c>.</value>
    public int MaxOperationsPerChunk { get; set; } = 150;

    /// <summary>
    /// Gets or sets a value indicating whether, in <see cref="BatchFailureMode.Atomic"/> mode, the
    /// remaining chunks are skipped once a chunk fails.
    /// </summary>
    /// <value>The default value is <c>true</c>.</value>
    public bool StopOnFirstFailedChunk { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="BatchFailureMode.ContinueOnError"/> is sent
    /// as a single <c>$batch</c> with the <c>Prefer: odata.continue-on-error</c> header rather than as
    /// independent per-item requests.
    /// </summary>
    /// <remarks>
    /// D365 F&amp;O does not document support for <c>odata.continue-on-error</c>, so this is disabled
    /// by default until verified against a live environment.
    /// </remarks>
    /// <value>The default value is <c>false</c>.</value>
    public bool UseServerContinueOnError { get; set; }
}
