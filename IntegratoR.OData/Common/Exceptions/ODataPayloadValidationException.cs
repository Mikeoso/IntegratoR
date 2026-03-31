namespace IntegratoR.OData.Common.Exceptions;

/// <summary>
/// Thrown when payload validation detects that required fields are missing before
/// sending the request to the OData service.
/// </summary>
public class ODataPayloadValidationException : Exception
{
    /// <summary>
    /// Gets the names of the required fields that were missing from the payload.
    /// </summary>
    public IReadOnlyList<string> MissingFields { get; }

    public ODataPayloadValidationException(string message, IReadOnlyList<string> missingFields)
        : base(message)
    {
        MissingFields = missingFields;
    }

    public ODataPayloadValidationException(string message, IReadOnlyList<string> missingFields, Exception innerException)
        : base(message, innerException)
    {
        MissingFields = missingFields;
    }
}
