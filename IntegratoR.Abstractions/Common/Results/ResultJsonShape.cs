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
    /// Throws on any non-<see cref="IntegrationError"/> because <see cref="IntegrationError"/>
    /// is the only <see cref="IError"/> implementation in this codebase. A defensive fallback
    /// would be dead code (per <c>common.md</c>: "do not add error handling for scenarios that
    /// cannot happen"); throwing makes any future violation loud and immediate.
    /// </summary>
    public static (string Code, string Message, ErrorType Type) Project(IError error)
    {
        if (error is not IntegrationError integrationError)
        {
            throw new InvalidOperationException(
                $"Unsupported IError type '{error.GetType().FullName}'. " +
                $"Only {nameof(IntegrationError)} can be serialised by {nameof(ResultJsonShape)}.");
        }

        return (integrationError.Code, integrationError.Message, integrationError.Type);
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
