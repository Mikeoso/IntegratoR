using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Domain.Enums.General;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace IntegratoR.OData.FO.Domain.Entities.Dimensions;

/// <summary>
/// Represents a configuration for formatting and parsing financial dimension strings in an integration context.
/// This entity is typically a custom or helper entity used to define how segmented dimension strings 
/// (e.g., "618160-001-023") should be constructed or deconstructed, mapping them to the correct dimension format in D365 F&O.
/// </summary>
[Table("DimensionIntegrationFormat")]
public class DimensionIntegrationFormat : BaseEntity<string>
{
    /// <summary>
    /// The unique name of the dimension format configuration. This serves as the primary identifier for the record.
    /// </summary>
    [Key]
    [JsonPropertyName("DimensionFormatName")]
    public required string DimensionFormatName { get; set; }

    /// <summary>
    /// The type of dimension hierarchy this format applies to, such as 'Dimension combination' or 'Ledger dimension format'.
    /// This determines the validation and structure rules used in D365 F&O.
    /// </summary>
    [Key]
    [JsonPropertyName("DimensionFormatType")]
    public DimensionHierarchyType DimensionFormatType { get; set; }

    /// <summary>
    /// A string defining the structure of the financial dimensions, typically using placeholders or dimension names.
    /// Example: "MainAccount-BusinessUnit-Department". This structure is used to correctly parse and build the dimension display value.
    /// </summary>
    [JsonPropertyName("FinancialDimensionFormat")]
    public virtual string? FinancialDimensionFormat { get; set; }

    /// <summary>
    /// Indicates whether this dimension format configuration is currently active and can be used by the integration.
    /// </summary>
    [JsonPropertyName("IsActive")]
    public virtual NoYes IsActive { get; set; }

    public override object[] GetCompositeKey()
    {
        return [DimensionFormatName, DimensionFormatType];
    }
}