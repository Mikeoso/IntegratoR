using IntegratoR.OData.FO.Domain.Enums.Dimensions;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file defines a strongly-typed configuration class for business-logic-specific settings
// related to Dynamics 365 Finance & Operations. It separates functional configuration
// (like financial dimension rules) from technical connection settings.
// </remarks>
// ---------------------------------------------------------------------------------------------
namespace IntegratoR.OData.FO.Domain.Models.Settings;

/// <summary>
/// Encapsulates configuration settings related to specific D365 Finance & Operations
/// business processes, with a focus on financial dimensions.
/// </summary>
/// <remarks>
/// This class is designed to be populated from a configuration source (e.g., `appsettings.json`)
/// using the .NET IOptions pattern. It provides a strongly-typed way to access functional
/// parameters required by application services.
/// </remarks>
public class FOSettings
{
    #region Financial Dimension Settings

    /// <summary>
    /// Gets or sets the name of the 'Financial dimension format' used to structure
    /// ledger account strings.
    /// </summary>
    /// <remarks>
    /// This name corresponds to a specific setup record in D365 F&O, found under
    /// **General ledger > Chart of accounts > Dimensions > Financial dimension formats**.
    /// The selected format defines which dimensions are used, their order, and the delimiter,
    /// which is essential for correctly constructing account combinations for transactions.
    /// </remarks>
    public string DimensionFormatName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of dimension hierarchy to use when processing dimension values.
    /// </summary>
    /// <seealso cref="DimensionHierarchyType"/>
    public DimensionHierarchyType DimensionHierarchyType { get; set; }

    #endregion
}