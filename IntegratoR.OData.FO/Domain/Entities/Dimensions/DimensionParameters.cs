using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;

namespace IntegratoR.OData.FO.Domain.Entities.Dimensions;

/// <summary>
/// Represents system-wide parameters for financial dimension handling in D365 F&amp;O, such as the segment delimiter.
/// </summary>
[Table("DimensionParameters")]
public class DimensionParameters : BaseEntity
{
    /// <summary>
    /// Gets or sets the primary key of the parameter record. D365 returns this as an integer singleton key.
    /// </summary>
    [Key]
    [JsonPropertyName("Key")]
    public required int Key { get; set; }

    /// <summary>
    /// Gets or sets the character used to separate segments within a financial dimension string, for example the hyphen in <c>618160-001-023</c>.
    /// </summary>
    [JsonPropertyName("DimensionSegmentDelimiter")]
    public virtual DimensionSegmentDelimiter DimensionSegmentDelimiter { get; set; }

    /// <summary>
    /// Gets the composite key formed from <see cref="Key"/>.
    /// </summary>
    public override object[] GetCompositeKey()
    {
        return [Key];
    }
}
