namespace IntegratoR.Abstractions.Common.Results;

/// <summary>
/// Categorises errors for HTTP status mapping and log level selection.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// Represents an unexpected or general failure.
    /// </summary>
    Failure,

    /// <summary>
    /// Represents a validation failure caused by invalid input.
    /// </summary>
    Validation,

    /// <summary>
    /// Represents a requested resource that could not be found.
    /// </summary>
    NotFound,

    /// <summary>
    /// Represents a conflict with the current state of a resource.
    /// </summary>
    Conflict
}
