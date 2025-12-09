using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.Common.Annotations;
using IntegratoR.OData.FO.Domain.Enums.General;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace IntegratoR.SampleFunction.Domain.Entities.Ledger;

/// <summary>
/// Mapping entity between Relion ledger accounts and main accounts in the erp system.
/// </summary>
[Table("RelionLedgerAccountMapping")]
public class LedgerAccountMapping : BaseEntity<string>
{
    /// <summary>
    /// Relion ledger account identifier.
    /// </summary>
    [Key]
    [JsonPropertyName("RelionLedgerIFRS")]
    [ODataField(IgnoreOnCreate = true)]
    public string? RelionLedgerIFRS { get; set; }

    /// <summary>
    /// Relion ledger account identifier.
    /// </summary>
    [Key]
    [Required]
    [JsonPropertyName("RelionLedgerAccount")]
    public required string RelionLedgerAccount { get; set; }

    /// <summary>
    /// Dynamics main account number mapped to the Relion ledger account.
    /// </summary>
    [JsonPropertyName("MainAccountNum")]
    public string? MainAccount { get; set; }

    /// <summary>
    /// Defines if this mapping should be excluded from import operations.
    /// </summary>
    [JsonPropertyName("NoYes")]
    public NoYes ExcludeFromImport { get; set; }

    public override object[] GetCompositeKey()
    {
        return [RelionLedgerIFRS!, RelionLedgerAccount];
    }
}
