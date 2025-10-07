using Newtonsoft.Json;

namespace IntegratoR.RELion.Domain.Models;

/// <summary>
/// Represents a ledger journal entry in Relion.
/// Contains details such as entry number, account information, posting details, and tax information.
/// </summary>
public class RelionLedgerJournalLine
{
    /// <summary>
    /// Gets or sets the unique entry number for the ledger journal entry.
    /// </summary>
    [JsonProperty("Entry No.")]
    public required int EntryNo { get; set; }

    /// <summary>
    /// Gets or sets the general ledger account number associated with the entry.
    /// </summary>
    [JsonProperty("G/L Account No.")]
    public required string AccountNum { get; set; }

    /// <summary>
    /// Gets or sets the posting date for the journal entry.
    /// </summary>
    [JsonProperty("Posting Date")]
    public required DateTimeOffset PostingDate { get; set; }

    /// <summary>
    /// Gets or sets the document number for the journal entry.
    /// </summary>
    [JsonProperty("Document No.")]
    public required string DocumentNo { get; set; }

    /// <summary>
    /// Gets or sets the description of the journal entry.
    /// </summary>
    [JsonProperty("Description")]
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the VAT (tax) amount for the journal entry.
    /// </summary>
    [JsonProperty("VAT Amount")]
    public decimal VatAmount { get; set; }

    /// <summary>
    /// Gets or sets the posting type for the journal entry (e.g., sales or purchase).
    /// </summary>
    [JsonProperty("Gen. Posting Type")]
    public int? PostingType { get; set; }

    /// <summary>
    /// Gets or sets the general business posting group for the entry.
    /// </summary>
    [JsonProperty("Gen. Bus. Posting Group")]
    public string? PostingGroup { get; set; }

    /// <summary>
    /// Gets or sets the debit amount for the journal entry.
    /// </summary>
    [JsonProperty("Debit Amount")]
    public decimal? DebitAmount { get; set; }

    /// <summary>
    /// Gets or sets the credit amount for the journal entry.
    /// </summary>
    [JsonProperty("Credit Amount")]
    public decimal? CreditAmount { get; set; }

    /// <summary>
    /// Gets or sets the document date associated with the journal entry.
    /// </summary>
    [JsonProperty("Document Date")]
    public DateTimeOffset? DocumentDate { get; set; }

    /// <summary>
    /// Gets or sets the external document number related to the journal entry.
    /// </summary>
    [JsonProperty("External Document No.")]
    public string? ExternalDocumentNo { get; set; }

    /// <summary>
    /// Gets or sets the VAT business posting group for the entry.
    /// </summary>
    [JsonProperty("VAT Bus. Posting Group")]
    public string? VATBusPostingGroup { get; set; }

    /// <summary>
    /// Gets or sets the VAT product posting group for the entry.
    /// </summary>
    [JsonProperty("VAT Prod. Posting Group")]
    public string? VATProdPostingGroup { get; set; }

    /// <summary>
    /// Gets or sets the intercompany partner code for the journal entry.
    /// </summary>
    [JsonProperty("IC Partner Code")]
    public required string ICPartnerCode { get; set; }

    /// <summary>
    /// Gets or sets the shortcut dimension code for the entry.
    /// </summary>
    [JsonProperty("Shortcut Dimension 8 Code")]
    public required string ShortcutDimensionCode { get; set; }

    [JsonProperty("Shortcut Dimension 4 Code")]
    public required string MovementType { get; set; }
    /// <summary>
    /// Gets or sets the related object number for the journal entry.
    /// </summary>
    [JsonProperty("RelC Object No.")]
    public required string RelObjectNum { get; set; }

    /// <summary>
    /// Gets or sets the related competence unit for the entry.
    /// </summary>
    [JsonProperty("RelC Competence Unit")]
    public required string RelCompetenceUnit { get; set; }

    /// <summary>
    /// Gets or sets additional description information for the entry.
    /// </summary>
    [JsonProperty("RelC Description 2")]
    public string? RelDescription { get; set; }
}
