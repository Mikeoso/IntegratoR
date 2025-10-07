using IntegratoR.Abstractions.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace IntegratoR.SampleFunction.Domain.Entities.Tax;

[Table("RelionItemTaxGroupMapping")]
public class ItemTaxGroupMapping : BaseEntity<string>
{
    [Key]
    [JsonPropertyName("INWRelionTaxBusinessBookingGroup")]
    public required string RelionTaxBusinessGroup { get; set; }

    [Key]
    [JsonPropertyName("INWRelionTaxProductBookingGroup")]
    public required string RelionTaxProductGroup { get; set; }

    [JsonPropertyName("TaxCode")]
    public string? TaxCode { get; set; }

    [JsonPropertyName("TaxItemGroup")]
    public string? TaxItemGroup { get; set; }

    public override string Id => $"{RelionTaxBusinessGroup}-{RelionTaxProductGroup}";
}
