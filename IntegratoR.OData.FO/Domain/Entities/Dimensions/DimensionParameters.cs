using IntegratoR.Abstractions.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace IntegratoR.OData.FO.Domain.Entities.Dimensions;

/// <summary>
/// Represents a custom entity for storing global parameters related to financial dimension handling within the integration.
/// This class defines system-wide settings, such as the delimiter used for parsing and constructing dimension strings,
/// ensuring consistent processing across different functions.
/// </summary>
[Table("DimensionParameters")]
public class DimensionParameters : BaseEntity<string>
{
    /// <summary>
    /// The primary key for the parameter record, used to uniquely identify this set of dimension settings.
    /// For example, this could be a predefined value like "Default".
    /// </summary>
    [Key]
    [JsonPropertyName("Key")]
    public string? Key { get; set; }

    /// <summary>
    /// Specifies the character used to separate segments within a financial dimension string.
    /// For instance, a hyphen ('-') is commonly used, as seen in "618160-001-023".
    /// </summary>
    [JsonPropertyName("DimensionSegmentDelimiter")]
    public virtual DimensionSegmentDelimiter DimensionSegmentDelimiter { get; set; }
}