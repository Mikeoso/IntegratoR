using IntegratoR.OData.Domain.Models;

namespace IntegratoR.OData.Common.Exceptions;

/// <summary>
/// Thrown when one or more operations within an OData batch request fail.
/// Contains per-entity failure details for diagnostic purposes.
/// </summary>
public class ODataBatchException : Exception
{
    /// <summary>
    /// Gets the per-entity operation results that failed.
    /// </summary>
    public IReadOnlyList<BatchOperationResult> FailedResults { get; }

    public ODataBatchException(string message, IReadOnlyList<BatchOperationResult> failedResults)
        : base(message)
    {
        FailedResults = failedResults;
    }

    public ODataBatchException(string message, IReadOnlyList<BatchOperationResult> failedResults, Exception innerException)
        : base(message, innerException)
    {
        FailedResults = failedResults;
    }
}
