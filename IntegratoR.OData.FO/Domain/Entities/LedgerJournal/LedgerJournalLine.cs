using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.Common.Annotations;
using IntegratoR.OData.FO.Domain.Enums.General;
using IntegratoR.OData.FO.Domain.Enums.LedgerJournals;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

/// <summary>
/// Represents a single line within a general journal in Dynamics 365 Finance and Operations.
/// This entity corresponds to the 'LedgerJournalLine' data entity and the underlying 'LedgerJournalTrans' table.
/// It is used to record debit or credit transactions against various account types such as Ledger, Customer, Vendor, etc.
/// </summary>
[Table("LedgerJournalLines")]
public class LedgerJournalLine : BaseEntity<string>
{
    /// <summary>
    /// The unique identifier of the legal entity (company) in which the journal line is created.
    /// Part of the composite primary key.
    /// </summary>
    [Key]
    [Required]
    [JsonPropertyName("dataAreaId")]
    public required string DataAreaId { get; set; }

    /// <summary>
    /// The identifier of the journal batch (header) to which this line belongs. Links to the 'LedgerJournalTable'.
    /// Part of the composite primary key.
    /// </summary>
    [Key]
    [Required]
    [JsonPropertyName("JournalBatchNumber")]
    public required string JournalBatchNumber { get; set; }

    /// <summary>
    /// The system-generated line number that provides a unique sequence for each line within a journal.
    /// This value is typically assigned by D365 F&O upon creation.
    /// Part of the composite primary key.
    /// </summary>
    [Key]
    [Required]
    [JsonPropertyName("LineNumber")]
    [ODataField(IgnoreOnCreate = true)]
    public decimal LineNumber { get; set; }

    /// <summary>
    /// The primary account for the transaction, combining the main account and financial dimensions.
    /// </summary>
    [Required]
    [JsonPropertyName("AccountDisplayValue")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual required string AccountDisplayValue { get; set; }

    /// <summary>
    /// The type of the primary account (e.g., Ledger, Customer, Vendor). Determines the business logic applied during posting.
    /// </summary>
    [JsonPropertyName("AccountType")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual LedgerJournalACType AccountType { get; set; }

    /// <summary>
    /// The debit amount of the transaction in the transaction currency. Must be zero if CreditAmount has a value.
    /// </summary>
    [Required]
    [JsonPropertyName("DebitAmount")]
    public virtual decimal DebitAmount { get; set; }

    /// <summary>
    /// The credit amount of the transaction in the transaction currency. Must be zero if DebitAmount has a value.
    /// </summary>
    [Required]
    [JsonPropertyName("CreditAmount")]
    public virtual decimal CreditAmount { get; set; }

    /// <summary>
    /// The three-letter ISO code for the currency of the transaction.
    /// </summary>
    [Required]
    [JsonPropertyName("CurrencyCode")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual required string CurrencyCode { get; set; }

    /// <summary>
    /// The offsetting account for the transaction, combining the main account and financial dimensions.
    /// </summary>
    [JsonPropertyName("OffsetAccountDisplayValue")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? OffsetAccountDisplayValue { get; set; }

    /// <summary>
    /// The type of the offsetting account (e.g., Ledger, Customer, Vendor).
    /// </summary>
    [JsonPropertyName("OffsetAccountType")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual LedgerJournalACType OffsetAccountType { get; set; }

    /// <summary>
    /// The legal entity (company) of the offsetting account. Required for intercompany transactions.
    /// </summary>
    [JsonPropertyName("OffsetCompany")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? OffsetCompany { get; set; }

    /// <summary>
    /// The transaction date, which determines the posting date for the financial entry.
    /// </summary>
    [Required]
    [JsonPropertyName("TransDate")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual required DateTimeOffset TransDate { get; set; }

    /// <summary>
    /// A descriptive text for the transaction line, which is carried through to the general ledger.
    /// </summary>
    [JsonPropertyName("Text")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? TransactionText { get; set; }

    /// <summary>
    /// The voucher number for the transaction. A single voucher can group multiple related journal lines (e.g., a debit and a credit).
    /// If not provided, it's typically assigned based on the journal's number sequence.
    /// </summary>
    [JsonPropertyName("Voucher")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? Voucher { get; set; }

    /// <summary>
    /// The financial dimensions associated with the main account.
    /// </summary>
    [JsonPropertyName("DefaultDimensionDisplayValue")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? DefaultDimensionDisplayValue { get; set; }

    /// <summary>
    /// The financial dimensions associated with the offset account.
    /// </summary>
    [JsonPropertyName("OffsetDefaultDimensionDisplayValue")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? OffsetDefaultDimensionDisplayValue { get; set; }

    /// <summary>
    /// The due date for the transaction, primarily used for customer and vendor account types to calculate payment terms.
    /// </summary>
    [Required]
    [JsonPropertyName("DueDate")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual DateTimeOffset DueDate { get; set; }

    /// <summary>
    /// The document date, often representing the date on an external document like a vendor invoice.
    /// </summary>
    [Required]
    [JsonPropertyName("DocumentDate")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual DateTimeOffset DocumentDate { get; set; }

    /// <summary>
    /// A reference to an external document, such as a vendor invoice number.
    /// </summary>
    [JsonPropertyName("Document")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? Document { get; set; }

    /// <summary>
    /// The customer invoice number or vendor invoice number associated with the transaction.
    /// </summary>
    [JsonPropertyName("Invoice")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? Invoice { get; set; }

    /// <summary>
    /// The posting profile used for customer or vendor transactions, which controls the summary accounts for posting.
    /// </summary>
    [JsonPropertyName("PostingProfile")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? PostingProfile { get; set; }

    /// <summary>
    /// The method of payment for the transaction, such as CHECK, EFT, etc.
    /// </summary>
    [JsonPropertyName("PaymentMethod")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? PaymentMethod { get; set; }

    /// <summary>
    /// A specific payment reference or note, often used for electronic fund transfers.
    /// </summary>
    [JsonPropertyName("PaymentReference")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? PaymentReference { get; set; }

    /// <summary>
    /// The exchange rate between the transaction currency and the accounting currency.
    /// </summary>
    [Required]
    [JsonPropertyName("ExchRate")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual decimal ExchRate { get; set; }

    /// <summary>
    /// The sales tax group associated with the transaction, which helps determine applicable taxes.
    /// </summary>
    [JsonPropertyName("SalesTaxGroup")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? SalesTaxGroup { get; set; }

    /// <summary>
    /// The item sales tax group, used in combination with the SalesTaxGroup to determine the specific sales tax code.
    /// </summary>
    [JsonPropertyName("ItemSalesTaxGroup")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? ItemSalesTaxGroup { get; set; }

    /// <summary>
    /// The specific sales tax code applied to the transaction line.
    /// </summary>
    [JsonPropertyName("SalesTaxCode")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? SalesTaxCode { get; set; }

    /// <summary>
    /// The tax-exempt number, used if the transaction is exempt from sales tax.
    /// </summary>
    [JsonPropertyName("TaxExemptNumber")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? TaxExemptNumber { get; set; }

    /// <summary>
    /// Indicates whether this line is a reversing entry.
    /// </summary>
    [JsonPropertyName("ReverseEntry")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual NoYes ReverseEntry { get; set; }

    /// <summary>
    /// The date on which the reversing entry will be posted. This field is mandatory if 'ReverseEntry' is set to Yes.
    /// </summary>
    [Required]
    [JsonPropertyName("ReverseDate")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual DateTimeOffset ReverseDate { get; set; }

    /// <summary>
    /// A client-side composite identifier constructed from DataAreaId, JournalBatchNumber, and LineNumber for simplified entity tracking.
    /// </summary>
    public override object[] GetCompositeKey()
    {
        return [DataAreaId, JournalBatchNumber, LineNumber];
    }
}