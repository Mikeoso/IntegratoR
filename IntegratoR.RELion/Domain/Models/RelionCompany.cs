using System.Text.Json.Serialization;

namespace IntegratoR.RELion.Domain.Models;

/// <summary>
/// Represents company information retrieved from the Relion API.
/// </summary>
public class RelionCompany
{
    /// <summary>
    /// Unique identifier for the company.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Name of the company.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Display name of the company.
    /// </summary>
    [JsonPropertyName("displayName")]
    public required string DisplayName { get; set; }
}
