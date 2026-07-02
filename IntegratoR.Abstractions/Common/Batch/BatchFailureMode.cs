namespace IntegratoR.Abstractions.Common.Batch;

/// <summary>
/// Controls how a batch write behaves when one of its operations fails.
/// </summary>
public enum BatchFailureMode
{
    /// <summary>
    /// All operations in a chunk run as a single OData changeset: either every operation commits
    /// or, if any fails, none of them are applied (all-or-nothing per chunk).
    /// </summary>
    Atomic = 0,

    /// <summary>
    /// Operations are applied independently: a failure does not stop the others, and the per-item
    /// outcome of every operation is reported so failed records can be identified and retried.
    /// </summary>
    ContinueOnError = 1,
}
