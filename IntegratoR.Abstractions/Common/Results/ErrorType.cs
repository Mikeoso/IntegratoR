namespace IntegratoR.Abstractions.Common.Results;

/// <summary>
/// Categorizes errors for HTTP status mapping and log level selection.
/// </summary>
public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict
}
