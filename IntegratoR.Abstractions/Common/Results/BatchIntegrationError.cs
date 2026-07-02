using IntegratoR.Abstractions.Common.Batch;

namespace IntegratoR.Abstractions.Common.Results;

/// <summary>
/// The header error of a failed batch write. Carries the full <see cref="BatchOutcome"/> so the
/// per-item failure list is retrievable from a failed <see cref="FluentResults.Result{T}"/> via
/// <c>result.GetError()</c>.
/// </summary>
public sealed class BatchIntegrationError : IntegrationError
{
    /// <summary>
    /// Gets the aggregate batch outcome, including the per-item success and failure detail.
    /// </summary>
    public BatchOutcome Outcome { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchIntegrationError"/> class.
    /// </summary>
    /// <param name="code">The machine-readable error code.</param>
    /// <param name="message">The human-readable summary (e.g. "3 of 200 operations failed").</param>
    /// <param name="outcome">The aggregate batch outcome carrying per-item detail.</param>
    /// <param name="type">The category used for HTTP status mapping. Defaults to <see cref="ErrorType.Failure"/>.</param>
    public BatchIntegrationError(string code, string message, BatchOutcome outcome, ErrorType type = ErrorType.Failure)
        : base(code, message, type)
    {
        Outcome = outcome;
    }
}
