using IntegratoR.OData.Domain.Models;

namespace IntegratoR.OData.Common.Exceptions;

/// <summary>
/// Represents the error that occurs when one or more operations within an OData batch request fail.
/// </summary>
[Obsolete("since v1.4.0; never thrown; batch failures surface via Result<T>; removed next MAJOR")]
public class ODataBatchException : Exception
{
    /// <summary>
    /// Gets the per-entity operation results that failed.
    /// </summary>
    public IReadOnlyList<BatchOperationResult> FailedResults { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataBatchException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="failedResults">The per-entity operation results that failed.</param>
    public ODataBatchException(string message, IReadOnlyList<BatchOperationResult> failedResults)
        : base(message)
    {
        FailedResults = failedResults;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataBatchException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="failedResults">The per-entity operation results that failed.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ODataBatchException(string message, IReadOnlyList<BatchOperationResult> failedResults, Exception innerException)
        : base(message, innerException)
    {
        FailedResults = failedResults;
    }
}
