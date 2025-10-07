namespace IntegratoR.Abstractions.Common.Results;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file defines the core components for a standardized error handling strategy across the integration solution.
// By using a structured `Error` record and a categorized `ErrorType`, we create a consistent and predictable
// way to represent failures. This decouples internal exceptions (e.g., an OData exception from D365) from the
// public error contract exposed by our APIs (e.g., in an Azure Function). This allows for robust logging,
// easier debugging, and clear, machine-readable error responses for client applications.
// </remarks>
// ---------------------------------------------------------------------------------------------

/// <summary>
/// Specifies the high-level category of an error, primarily used to map a business or system failure
/// to a corresponding and conventional HTTP status code in the API layer.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// Indicates a general or unexpected failure in the application.
    /// </summary>
    /// <remarks>
    /// This type typically maps to an <b>HTTP 500 Internal Server Error</b>.
    /// </remarks>
    Failure = 0,

    /// <summary>
    /// Indicates that the request could not be processed due to invalid syntax or semantic errors in the input data.
    /// </summary>
    /// <remarks>
    /// This type typically maps to an <b>HTTP 400 Bad Request</b>.
    /// </remarks>
    Validation = 1,

    /// <summary>
    /// Indicates that a requested resource could not be found at the specified location.
    /// </summary>
    /// <remarks>
    /// This type typically maps to an <b>HTTP 404 Not Found</b>.
    /// </remarks>
    NotFound = 2,

    /// <summary>
    /// Indicates that the request could not be completed due to a conflict with the current state of the target resource.
    /// For example, attempting to create a resource that already exists.
    /// </summary>
    /// <remarks>
    /// This type typically maps to an <b>HTTP 409 Conflict</b>.
    /// </remarks>
    Conflict = 3
}

/// <summary>
/// Represents a specific, structured error containing a machine-readable code, a human-readable message, and a category.
/// This record serves as the standard Data Transfer Object (DTO) for failure information across all application layers.
/// </summary>
/// <param name="Code">A stable, unique error code for programmatic handling by clients (e.g., "Customers.DuplicateEmail"). This should not change between versions.</param>
/// <param name="Message">A descriptive, human-readable message intended for developers and logging. It should not be parsed by client applications and may change over time.</param>
/// <param name="Type">The category of the error, which dictates the nature of the failure and informs the resulting HTTP status code.</param>
/// <param name="Exception">The optional, underlying exception that caused this error. This is crucial for logging and debugging but must never be serialized to the client.</param>
public sealed record Error(string Code, string Message, ErrorType Type, Exception? Exception = null);