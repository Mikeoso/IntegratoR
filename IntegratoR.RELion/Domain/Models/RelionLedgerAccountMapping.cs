using Newtonsoft.Json;

namespace IntegratoR.RELion.Domain.Models;

/// <summary>
/// Represents the association between a tax transaction and a ledger journal line
/// </summary>
public class RelionLedgerAccountMapping
{
    /// <summary>
    /// The ledger journal entry number
    /// </summary>
    [JsonProperty("G/L Entry No.")]
    public required string LedgerAccountNo { get; set; }

    /// <summary>
    /// The tax transaction number
    /// </summary>
    [JsonProperty("VAT Entry No.")]
    public required string TaxAccountNo { get; set; }
}
