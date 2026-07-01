namespace IntegratoR.Abstractions.Common.Results;

/// <summary>
/// A domain-specific error that extends <see cref="FluentResults.Error"/> with
/// a machine-readable <see cref="Code"/> and an <see cref="ErrorType"/> for HTTP status mapping.
/// </summary>
public class IntegrationError : FluentResults.Error
{
    /// <summary>
    /// Gets the machine-readable error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the category used for HTTP status mapping.
    /// </summary>
    public ErrorType Type { get; }

    /// <summary>
    /// Gets the exception that caused this error, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationError"/> class.
    /// </summary>
    /// <param name="code">The machine-readable error code.</param>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="type">The category used for HTTP status mapping.</param>
    /// <param name="exception">The exception that caused this error, if any.</param>
    public IntegrationError(string code, string message, ErrorType type, Exception? exception = null)
        : base(message)
    {
        Code = code;
        Type = type;
        Exception = exception;

        if (exception is not null)
            CausedBy(exception);
    }
}
