using FluentResults;

namespace IntegratoR.Abstractions.Common.Results;

/// <summary>
/// Serializer-agnostic shape definition for the JSON representation of <see cref="Result"/>
/// and <see cref="Result{T}"/>. Holds the property-name constants and the projection/hydration
/// helpers shared by the Newtonsoft.Json and System.Text.Json converter families so the JSON
/// shape stays in lockstep across both serialisers.
/// </summary>
internal static class ResultJsonShape
{
    public const string IsSuccess = "isSuccess";
    public const string Value = "value";
    public const string Errors = "errors";
    public const string Code = "code";
    public const string Message = "message";
    public const string Type = "type";

    private const string UnknownCode = "Unknown";
    private const string UnknownMessage = "Unknown error";

    /// <summary>
    /// Projects an <see cref="IError"/> into the three primitives both converters write.
    /// Preserves the richer <see cref="IntegrationError"/> fields when available, and falls
    /// back to <c>(Unknown, error.Message, Failure)</c> for any other <see cref="IError"/>
    /// so library consumers using <c>Result.Fail("...")</c> with a plain error continue to
    /// round-trip without throwing. The Newtonsoft converters in this assembly are public
    /// API and have always accepted any <see cref="IError"/>; preserving that contract avoids
    /// an unannounced breaking change for downstream callers.
    /// </summary>
    public static (string Code, string Message, ErrorType Type) Project(IError error)
    {
        if (error is IntegrationError integrationError)
        {
            return (integrationError.Code, integrationError.Message, integrationError.Type);
        }

        return (UnknownCode, string.IsNullOrEmpty(error.Message) ? UnknownMessage : error.Message, ErrorType.Failure);
    }

    /// <summary>
    /// Reconstructs an <see cref="IntegrationError"/> from JSON primitives, applying fallbacks
    /// for missing fields so partial or older JSON payloads still deserialise to a usable error.
    /// </summary>
    public static IntegrationError Hydrate(string? code, string? message, string? type)
    {
        ErrorType parsedType = ErrorType.Failure;
        if (!string.IsNullOrEmpty(type) && Enum.TryParse(type, out ErrorType parsed))
        {
            parsedType = parsed;
        }

        return new IntegrationError(
            code ?? UnknownCode,
            message ?? UnknownMessage,
            parsedType);
    }
}
