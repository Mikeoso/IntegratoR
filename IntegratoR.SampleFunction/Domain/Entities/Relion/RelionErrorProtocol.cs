using IntegratoR.Abstractions.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace IntegratoR.SampleFunction.Domain.Entities.Relion;

[Table("RelionErrorProtocol")]
public class RelionErrorProtocol : BaseEntity<string>
{
    [Key]
    [Required]
    [JsonPropertyName("dataAreaId")]
    public required string DataAreaId { get; set; }

    [Key]
    [Required]
    [JsonPropertyName("ErrorNum")]
    public required string ErrorNum { get; set; }

    [Key]
    [Required]
    [JsonPropertyName("ErrorDescription")]
    public required string ErrorDescription { get; set; }

    [JsonPropertyName("ErrorPayload")]
    public string? ErrorPayload { get; set; }

    [JsonIgnore]
    public override string Id => $"{DataAreaId}-{ErrorNum}-{ErrorDescription}";
}
