using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;

namespace IntegratoR.OData.FO.Builders;

/// <summary>
/// A builder class that constructs formatted financial dimension strings compatible with Dynamics 365 F&O.
/// It uses a fluent interface to ensure dimension values are assembled in the correct order as defined
/// by the system's dimension format, correctly handling delimiters and omitted values.
/// </summary>
/// <example>
/// <code>
/// // 1. Define the dimension format, typically loaded from F&O.
/// var format = new DimensionFormat
/// {
///     Delimiter = "-",
///     Segments = new List<string> { "BusinessUnit", "Department", "CostCenter" }
/// };
///
/// // 2. Use the builder to construct the dimension string.
/// var dimensionBuilder = new FinancialDimensionBuilder();
/// string displayValue = dimensionBuilder
///     .Initialize(format)
///     .Add("CostCenter", "CC002")
///     .Add("BusinessUnit", "BU01")
///     .Build();
///
/// // 3. The output respects the segment order and handles the missing "Department" value.
/// // Expected output: "BU01--CC002"
/// </code>
/// </example>
public class FinancialDimensionBuilder
{
    private readonly Dictionary<string, string> _dimensions = new();
    private DimensionFormat? _format;

    /// <summary>
    /// Initializes the builder with the dimension format that dictates the structure of the output string.
    /// This method should be called first and also resets the builder's state.
    /// </summary>
    /// <param name="format">The <see cref="DimensionFormat"/> object defining the segment order and delimiter.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public FinancialDimensionBuilder Initialize(DimensionFormat format)
    {
        Clear();
        _format = format;
        return this;
    }

    /// <summary>
    /// Adds or updates a financial dimension segment with its value. The order of adding dimensions does not matter.
    /// </summary>
    /// <param name="name">The name of the dimension segment (e.g., "BusinessUnit").</param>
    /// <param name="value">The value of the dimension segment (e.g., "001").</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public FinancialDimensionBuilder Add(string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
        {
            _dimensions[name] = value;
        }
        return this;
    }

    /// <summary>
    /// Constructs the final, delimited string using only the dimension values in the correct order.
    /// </summary>
    /// <returns>A formatted string (e.g., "BU01--CC002") or an empty string if the builder was not initialized.</returns>
    /// <remarks>
    /// This method correctly handles omitted dimension values by inserting an empty placeholder, which is required
    /// by D365 F&O to maintain the structural integrity of the dimension string (e.g., producing "value1--value3"
    /// if the middle segment was not provided).
    /// </remarks>
    public string Build()
    {
        if (_format is null || !_format.Segments.Any())
        {
            return string.Empty;
        }

        var valueParts = new List<string>();

        // Iterate through the segments in the exact order defined by the format.
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
    /// Resets the builder to its initial state by clearing all added dimensions and the format.
    /// This allows the builder instance to be reused for constructing multiple dimension strings.
    /// </summary>
    public void Clear()
    {
        _dimensions.Clear();
        _format = null;
    }
}