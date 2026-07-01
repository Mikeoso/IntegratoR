using IntegratoR.OData.FO.Domain.Enums.Dimensions;

namespace IntegratoR.OData.FO.Domain.Models.Settings;

/// <summary>
/// Represents the D365 Finance &amp; Operations configuration settings, focused on financial dimensions,
/// bound from configuration via the .NET options pattern.
/// </summary>
public class FOSettings
{
    #region Financial Dimension Settings

    /// <summary>
    /// Gets or sets the name of the financial dimension format used to structure ledger account strings.
    /// </summary>
    /// <remarks>
    /// Corresponds to a setup record in D365 F&amp;O under General ledger &gt; Chart of accounts &gt;
    /// Dimensions &gt; Financial dimension formats, which defines the dimensions, their order, and the delimiter.
    /// </remarks>
    public string DimensionFormatName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of dimension hierarchy to use when processing dimension values.
    /// </summary>
    /// <seealso cref="DimensionHierarchyType"/>
    public DimensionHierarchyType DimensionHierarchyType { get; set; }

    #endregion
}
