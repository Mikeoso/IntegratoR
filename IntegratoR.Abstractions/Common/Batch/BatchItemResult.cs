namespace IntegratoR.Abstractions.Common.Batch;

/// <summary>
/// The outcome of a single operation within a batch write, carrying enough detail to identify and
/// re-drive a failed record during an import.
/// </summary>
public sealed record BatchItemResult
{
    /// <summary>
    /// Gets the zero-based position of this operation in the original input sequence, across all chunks.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// Gets the zero-based index of the chunk this operation was submitted in.
    /// </summary>
    public required int ChunkIndex { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation completed successfully (an HTTP 2xx status).
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the HTTP status code D365 returned for this operation.
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    /// Gets the composite key of the record the operation targeted (wire field names to values), or
    /// <c>null</c> for a create whose key D365 assigns server-side.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Key { get; init; }

    /// <summary>
    /// Gets the machine-readable D365 inner-error code for a failed operation, if one was returned.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Gets the human-readable error message for a failed operation, if any.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
