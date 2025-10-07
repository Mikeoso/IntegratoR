using IntegratoR.RELion.Domain.DTOs;
using Newtonsoft.Json;

namespace IntegratoR.RELion.Domain.DTOs;

/// <summary>
/// Represents the request payload sent to the Relion API.
/// </summary>
public class RelionRequest
{
    /// <summary>
    /// Represents the table number in Relion to query.
    /// </summary>
    [JsonProperty("tableNo")]
    public required string TableNumber { get; set; }

    /// <summary>
    /// Represents the operation to perform, e.g., "READ".
    /// </summary>
    [JsonProperty("operation")]
    public string Operation { get; set; } = "READ";

    /// <summary>
    /// Represents whether to run triggers associated with the operation.
    /// </summary>
    [JsonProperty("runTrigger")]
    public bool RunTrigger { get; set; } = true;

    /// <summary>
    /// Represents the setup code for the request, if any.
    /// </summary>
    [JsonProperty("setupCode")]
    public string SetupCode { get; set; } = string.Empty;

    /// <summary>
    /// Defines the maximum number of records to return in the response.
    /// </summary>
    [JsonProperty("top")]
    public int Top { get; set; }

    /// <summary>
    /// Defines the number of records to skip before starting to return records.
    /// </summary>
    [JsonProperty("skip")]
    public int Skip { get; set; }

    /// <summary>
    /// Defines the list of filters and operations to apply in the request.
    /// </summary>
    [JsonProperty("entitySet")]
    public List<RelionRequestFilter> EntitySet { get; set; } = new();
}
