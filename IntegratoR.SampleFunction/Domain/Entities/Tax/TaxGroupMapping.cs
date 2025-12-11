using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.SampleFunction.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace IntegratoR.SampleFunction.Domain.Entities.Tax;

[Table("RelionTaxGroupMapping")]
public class TaxGroupMapping : BaseEntity<string>
{
    [Key]
    [JsonPropertyName("RelionBookingType")]
    public required RelionBookingType RelionBookingType { get; set; }

    [Key]
    [JsonPropertyName("RelionBusinessBookingGroup")]
    public required string RelionBusinessGroup { get; set; }

    [JsonPropertyName("TaxGroup")]
    public string? TaxGroup { get; set; }

    public override object[] GetCompositeKey()
    {
        return [RelionBookingType, RelionBusinessGroup];
    }
}
