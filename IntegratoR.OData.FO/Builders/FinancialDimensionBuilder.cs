using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;

namespace IntegratoR.OData.FO.Builders;

/// <summary>
/// Provides a fluent builder that assembles formatted financial dimension strings for D365 F&amp;O,
/// ordering segments per the dimension format and inserting empty placeholders for omitted values.
/// </summary>
/// <example>
/// <code>
/// var format = new DimensionFormat
/// {
///     Delimiter = "-",
///     Segments = new List&lt;string&gt; { "BusinessUnit", "Department", "CostCenter" }
/// };
///
/// string displayValue = new FinancialDimensionBuilder()
///     .Initialize(format)
///     .Add("CostCenter", "CC002")
///     .Add("BusinessUnit", "BU01")
///     .Build();
///
/// // displayValue => "BU01--CC002" (empty placeholder for the omitted "Department")
/// </code>
/// </example>
public class FinancialDimensionBuilder
{
    private readonly Dictionary<string, string> _dimensions = new();
    private DimensionFormat? _format;

    /// <summary>
    /// Resets the builder and sets the dimension format that dictates the output structure.
    /// </summary>
    /// <param name="format">The <see cref="DimensionFormat"/> defining the segment order and delimiter.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    public FinancialDimensionBuilder Initialize(DimensionFormat format)
    {
        Clear();
        _format = format;
        return this;
    }

    /// <summary>
    /// Adds or updates a dimension segment value; the order in which segments are added is irrelevant.
    /// </summary>
    /// <param name="name">The name of the dimension segment (e.g. "BusinessUnit").</param>
    /// <param name="value">The value of the dimension segment (e.g. "001").</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    public FinancialDimensionBuilder Add(string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
        {
            _dimensions[name] = value;
        }
        return this;
    }

    /// <summary>
    /// Builds the delimited dimension string with segments in the order defined by the format.
    /// </summary>
    /// <returns>The formatted string (e.g. "BU01--CC002"), or an empty string if the builder was not initialised.</returns>
    /// <remarks>
    /// Omitted segments are emitted as empty placeholders, which D365 F&amp;O requires to preserve the
    /// structural integrity of the dimension string.
    /// </remarks>
    public string Build()
    {
        if (_format is null || !_format.Segments.Any())
        {
            return string.Empty;
        }

        var valueParts = new List<string>();

        foreach (var segmentName in _format.Segments)
        {
            if (_dimensions.TryGetValue(segmentName, out var value))
            {
                valueParts.Add(value);
            }
            else
            {
                valueParts.Add(string.Empty);
            }
        }
        return string.Join(_format.Delimiter, valueParts);
    }

    /// <summary>
    /// Clears all added dimensions and the format so the builder instance can be reused.
    /// </summary>
    public void Clear()
    {
        _dimensions.Clear();
        _format = null;
    }
}
