using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.Common.Annotations;
using IntegratoR.OData.FO.Domain.Enums.General;
using IntegratoR.OData.FO.Domain.Enums.LedgerJournals;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

/// <summary>
/// Represents the header of a general journal in Dynamics 365 Finance and Operations.
/// This entity corresponds to the 'LedgerJournalHeader' data entity and the underlying 'LedgerJournalTable' table.
/// It acts as a container for a batch of journal lines, defining common properties and controlling the overall posting process.
/// </summary>
[Table("LedgerJournalHeaders")]
public class LedgerJournalHeader : BaseEntity<string>
{
    /// <summary>
    /// The unique identifier of the legal entity (company) in which the journal is created.
    /// Part of the composite primary key.
    /// </summary>
    [Key]
    [JsonPropertyName("dataAreaId")]
    public required string DataAreaId { get; set; }

    /// <summary>
    /// The unique identifier for the journal batch. This value is typically generated automatically by a number sequence
    /// defined on the associated Journal Name in D365 F&O. Part of the composite primary key.
    /// </summary>
    [Key]
    [JsonPropertyName("JournalBatchNumber")]
    [ODataField(IgnoreOnCreate = true)]
    public string? JournalBatchNumber { get; set; }

    /// <summary>
    /// The identifier for the Journal Name setup. This is a crucial field as it governs the journal's behavior,
    /// including default values, number sequences, posting restrictions, and workflow configurations.
    /// </summary>
    [JsonPropertyName("JournalName")]
    public virtual required string JournalName { get; set; }

    /// <summary>
    /// A user-defined description for the journal batch, providing context for its contents.
    /// </summary>
    [JsonPropertyName("Description")]
    public virtual required string Description { get; set; }

    /// <summary>
    /// A custom field used to store a unique identifier from an external system.
    /// This key is essential for tracking and ensuring idempotency in integration scenarios.
    /// </summary>
    [JsonPropertyName("IntegrationKey")]
    public virtual string? IntegrationKey { get; set; }

    /// <summary>
    /// Specifies the financial posting layer for the journal's transactions (e.g., Current, Operations, Tax).
    /// Posting layers allow for multiple accounting representations for a single transaction.
    /// </summary>
    [JsonPropertyName("PostingLayer")]
    public virtual CurrentOperationsTax PostingLayer { get; set; }

    /// <summary>
    /// A read-only status flag indicating whether the journal has been successfully posted to the general ledger.
    /// </summary>
    [JsonPropertyName("IsPosted")]
    public virtual NoYes IsPosted { get; set; }

    /// <summary>
    // A read-only, system-calculated field showing the total of all debit amounts from the journal lines.
    /// </summary>
    [Required]
    [JsonPropertyName("JournalTotalDebit")]
    public virtual decimal JournalTotalDebit { get; set; }

    /// <summary>
    /// A read-only, system-calculated field showing the total of all credit amounts from the journal lines.
    /// </summary>
    [Required]
    [JsonPropertyName("JournalTotalCredit")]
    public virtual decimal JournalTotalCredit { get; set; }

    /// <summary>
    /// The accounting currency of the legal entity, which is the base currency for the journal's transactions. This is typically a read-only field.
    /// </summary>
    [JsonPropertyName("AccountingCurrency")]
    public virtual string? AccountingCurrency { get; set; }

    /// <summary>
    /// A client-side composite identifier constructed from DataAreaId and JournalBatchNumber for simplified entity tracking within applications.
    /// </summary>
    public override object[] GetCompositeKey()
    {
        return [DataAreaId, JournalBatchNumber ?? "null"];
    }
}