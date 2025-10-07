using Newtonsoft.Json;

namespace IntegratoR.RELion.Domain.DTOs;

/// <summary>
/// Represents a single entity in the response from the Relion API.
/// </summary>
public class RelionResponseEntity
{
    /// <summary>
    /// Defines if there are more rows available in the response.
    /// </summary>
    [JsonProperty("moreRows")]
    public bool MoreRows { get; set; }

    /// <summary>
    /// Represents the total number of rows returned in the response.
    /// </summary>
    [JsonProperty("numberOfRows")]
    public int NumberOfRows { get; set; }

    /// <summary>
    /// Represents the number of rows that were skipped in the response.
    /// </summary>
    [JsonProperty("skippedRows")]
    public int SkippedRows { get; set; }

    /// <summary>
    /// The actual response data in JSON format, encoded as a string.
    /// </summary>
    [JsonProperty("ResponseJson2")]
    public string? EncodedResponseJson { get; set; }
}
