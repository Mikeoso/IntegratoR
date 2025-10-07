using Newtonsoft.Json;

namespace IntegratoR.RELion.Domain.DTOs;

/// <summary>
/// Generic data wrapper for Relion responses.
/// </summary>
/// <typeparam name="T"></typeparam>
public class RelionDataWrapper<T>
{
    /// <summary>
    /// The actual data returned from Relion.
    /// </summary>
    [JsonProperty("Data")]
    public List<T> Data { get; set; } = new();
}
