namespace IntegratoR.OData.FO.Domain.Models.FinancialDimensions;

/// <summary>
/// Represents the parsed structure of a financial dimension format: its delimiter and the ordered
/// list of segment names (e.g. "MainAccount-BusinessUnit-Department").
/// </summary>
public class DimensionFormat
{
    /// <summary>
    /// Gets or sets the string used to separate the dimension segments (e.g. "-").
    /// </summary>
    public required string Delimiter { get; set; }

    /// <summary>
    /// Gets or sets the ordered list of dimension segment names as they appear in the format
    /// (e.g. "MainAccount", "BusinessUnit", "Department").
    /// </summary>
    public List<string> Segments { get; set; } = new();
}
