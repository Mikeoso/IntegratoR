using Newtonsoft.Json;

namespace IntegratoR.RELion.Domain.DTOs;

/// <summary>
/// Represents a single filter or operation within the Relion request payload.
/// </summary>
[JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
public class RelionRequestFilter
{
    /// <summary>
    /// Defines the field number in Relion to apply the filter or operation on.
    /// </summary>
    [JsonProperty("fieldNo")]
    public string? FieldNumber { get; set; }

    /// <summary>
    /// Represents the operation to perform, e.g., "EQ" for equals, "NE" for not equals, etc.
    /// </summary>
    [JsonProperty("filter")]
    public bool? Filter { get; set; }

    /// <summary>
    /// Represents the value to compare against in the filter operation.
    /// </summary>
    [JsonProperty("value")]
    public string? Value { get; set; }

    /// <summary>
    /// Represents a sub-operation to perform, e.g., "AND", "OR".
    /// </summary>
    [JsonProperty("subOperation")]
    public string? SubOperation { get; set; }

    /// <summary>
    /// The fields to include in the response, separated by '|'.
    /// </summary>
    [JsonProperty("responseFields")]
    public string? ResponseFields { get; set; }
}
