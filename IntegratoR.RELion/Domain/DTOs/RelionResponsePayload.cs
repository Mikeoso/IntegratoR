using IntegratoR.RELion.Domain.DTOs;
using Newtonsoft.Json;

namespace IntegratoR.RELion.Domain.DTOs;

/// <summary>
/// The payload of the response from the Relion API.
/// </summary>
public class RelionResponsePayload
{
    /// <summary>
    /// Represents the set of entities returned in the response.
    /// </summary>
    [JsonProperty("entitySet")]
    public List<RelionResponseEntity> EntitySet { get; set; } = new();
}
