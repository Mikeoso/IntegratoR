using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;

namespace IntegratoR.OData.FO.Builders;

/// <summary>
/// Parses formatted financial dimension strings back into named segment values.
/// This is the inverse of <see cref="FinancialDimensionBuilder"/> — given a dimension string
/// like "BU01--CC002" and its <see cref="DimensionFormat"/>, it produces a dictionary mapping
/// each segment name to its value (including empty strings for omitted segments).
/// </summary>
/// <example>
/// <code>
/// var format = new DimensionFormat
/// {
///     Delimiter = "-",
///     Segments = new List&lt;string&gt; { "BusinessUnit", "Department", "CostCenter" }
/// };
///
/// Result&lt;Dictionary&lt;string, string&gt;&gt; result =
///     FinancialDimensionReader.Parse(format, "BU01--CC002");
///
/// // result.Value => { "BusinessUnit": "BU01", "Department": "", "CostCenter": "CC002" }
/// </code>
/// </example>
public static class FinancialDimensionReader
{
    /// <summary>
    /// Parses a formatted dimension string into a dictionary of segment name to value pairs.
    /// Empty segments are preserved as empty strings to enable lossless round-tripping with
    /// <see cref="FinancialDimensionBuilder"/>.
    /// </summary>
    /// <param name="format">The <see cref="DimensionFormat"/> defining the segment order and delimiter.</param>
    /// <param name="dimensionString">The formatted dimension string to parse (e.g., "BU01--CC002").</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing a dictionary mapping segment names to their values,
    /// or a failed result if the format is invalid, the input is empty, or the segment count does not match.
    /// </returns>
    public static Result<Dictionary<string, string>> Parse(DimensionFormat format, string dimensionString)
    {
        if (format is null || format.Segments.Count == 0)
        {
            return Result.Fail<Dictionary<string, string>>(new IntegrationError(
                "DimensionReader.InvalidFormat",
                "The dimension format is null or has no segments.",
                ErrorType.Validation));
        }

        if (string.IsNullOrEmpty(dimensionString))
        {
            return Result.Fail<Dictionary<string, string>>(new IntegrationError(
                "DimensionReader.EmptyInput",
                "The dimension string is null or empty.",
                ErrorType.Validation));
        }

        string[] parts = dimensionString.Split(format.Delimiter);

        if (parts.Length != format.Segments.Count)
        {
            return Result.Fail<Dictionary<string, string>>(new IntegrationError(
                "DimensionReader.SegmentCountMismatch",
                $"Expected {format.Segments.Count} segments but the dimension string contains {parts.Length}.",
                ErrorType.Validation));
        }

        Dictionary<string, string> result = new(format.Segments.Count);

        for (int i = 0; i < format.Segments.Count; i++)
        {
            result[format.Segments[i]] = parts[i];
        }

        return Result.Ok(result);
    }
}
