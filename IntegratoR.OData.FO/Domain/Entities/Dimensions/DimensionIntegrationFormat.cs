using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Domain.Enums.General;

namespace IntegratoR.OData.FO.Domain.Entities.Dimensions;

/// <summary>
/// Represents a configuration for formatting and parsing segmented financial dimension strings for integration with D365 F&amp;O.
/// </summary>
[Table("DimensionIntegrationFormats")]
public class DimensionIntegrationFormat : BaseEntity
{
    /// <summary>
    /// Gets or sets the unique name of the dimension format configuration. Part of the composite key.
    /// </summary>
    [Key]
    [JsonPropertyName("DimensionFormatName")]
    public required string DimensionFormatName { get; set; }

    /// <summary>
    /// Gets or sets the dimension hierarchy type this format applies to, determining the D365 F&amp;O validation and structure rules. Part of the composite key.
    /// </summary>
    [Key]
    [JsonPropertyName("DimensionFormatType")]
    public DimensionHierarchyType DimensionFormatType { get; set; }

    /// <summary>
    /// Gets or sets the structure of the financial dimensions, for example <c>MainAccount-BusinessUnit-Department</c>.
    /// </summary>
    [JsonPropertyName("FinancialDimensionFormat")]
    public virtual string? FinancialDimensionFormat { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this dimension format configuration is active.
    /// </summary>
    [JsonPropertyName("IsActive")]
    public virtual NoYes IsActive { get; set; }

    /// <summary>
    /// Gets the composite key formed from <see cref="DimensionFormatName"/> and <see cref="DimensionFormatType"/>.
    /// </summary>
    public override object[] GetCompositeKey()
    {
        return [DimensionFormatName, DimensionFormatType];
    }
}
