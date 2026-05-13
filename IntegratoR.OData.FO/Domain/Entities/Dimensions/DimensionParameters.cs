using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;

namespace IntegratoR.OData.FO.Domain.Entities.Dimensions;

/// <summary>
/// Represents a custom entity for storing global parameters related to financial dimension handling within the integration.
/// This class defines system-wide settings, such as the delimiter used for parsing and constructing dimension strings,
/// ensuring consistent processing across different functions.
/// </summary>
[Table("DimensionParameters")]
public class DimensionParameters : BaseEntity<int>
{
    /// <summary>
    /// The primary key for the parameter record, used to uniquely identify this set of dimension settings.
    /// In D365 F&amp;O this is an integer (likely the underlying value of an X++ enum used as a
    /// singleton key) — verified live against the JFI sandbox 2026-04-27, which returned
    /// <c>"Key": 0</c> as a JSON number. The original CLR declaration as <c>string</c> was wrong
    /// and caused <c>JsonException</c> on every <c>FindAll</c> call against this entity.
    /// </summary>
    [Key]
    [JsonPropertyName("Key")]
    public required int Key { get; set; }

    /// <summary>
    /// Specifies the character used to separate segments within a financial dimension string.
    /// For instance, a hyphen ('-') is commonly used, as seen in "618160-001-023".
    /// </summary>
    [JsonPropertyName("DimensionSegmentDelimiter")]
    public virtual DimensionSegmentDelimiter DimensionSegmentDelimiter { get; set; }

    public override object[] GetCompositeKey()
    {
        return [Key];
    }
}
