namespace IntegratoR.OData.Domain.Models;

/// <summary>
/// Represents the result of a single operation within a batch request.
/// </summary>
public sealed record BatchOperationResult
{
    /// <summary>
    /// Gets the zero-based index of the operation within the batch.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// Gets the HTTP status code returned for this operation.
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    /// Gets a value indicating whether this operation succeeded.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the error message if the operation failed; otherwise <c>null</c>.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the raw response body for this operation, if available.
    /// </summary>
    public string? ResponseBody { get; init; }
}
