namespace IntegratoR.Abstractions.Common.Results;

/// <summary>
/// A domain-specific error that extends <see cref="FluentResults.Error"/> with
/// a machine-readable <see cref="Code"/> and an <see cref="ErrorType"/> for HTTP status mapping.
/// </summary>
public class IntegrationError : FluentResults.Error
{
    public string Code { get; }
    public ErrorType Type { get; }
    public Exception? Exception { get; }

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
