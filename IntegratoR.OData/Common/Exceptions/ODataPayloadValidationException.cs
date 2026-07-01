namespace IntegratoR.OData.Common.Exceptions;

/// <summary>
/// Represents the error that occurs when payload validation detects required fields missing before the request is sent to the OData service.
/// </summary>
public class ODataPayloadValidationException : Exception
{
    /// <summary>
    /// Gets the names of the required fields that were missing from the payload.
    /// </summary>
    public IReadOnlyList<string> MissingFields { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataPayloadValidationException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="missingFields">The names of the required fields that were missing from the payload.</param>
    public ODataPayloadValidationException(string message, IReadOnlyList<string> missingFields)
        : base(message)
    {
        MissingFields = missingFields;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataPayloadValidationException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="missingFields">The names of the required fields that were missing from the payload.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ODataPayloadValidationException(string message, IReadOnlyList<string> missingFields, Exception innerException)
        : base(message, innerException)
    {
        MissingFields = missingFields;
    }
}
