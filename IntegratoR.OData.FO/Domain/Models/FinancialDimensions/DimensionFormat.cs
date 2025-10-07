namespace IntegratoR.OData.FO.Domain.Models.FinancialDimensions;

/// <summary>
/// Represents the parsed structure of a financial dimension format.
/// This is a helper class used within the integration logic to hold the components
/// of a dimension format string, such as the delimiter and the ordered list of dimension segment names.
/// For a format string like 'MainAccount-BusinessUnit-Department', this class would store '-' as the Delimiter and
/// a list containing 'MainAccount', 'BusinessUnit', and 'Department' as the Segments.
/// </summary>
public class DimensionFormat
{
    /// <summary>
    /// The character or string used to separate the dimension segments (e.g., '-').
    /// </summary>
    public required string Delimiter { get; set; }

    /// <summary>
    /// An ordered list of the names of the financial dimension segments as they appear in the format
    /// (e.g., ["MainAccount", "BusinessUnit", "Department"]).
    /// </summary>
    public List<string> Segments { get; set; } = new();
}
