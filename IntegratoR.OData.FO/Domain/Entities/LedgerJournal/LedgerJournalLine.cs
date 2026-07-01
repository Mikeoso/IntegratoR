using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.Common.Annotations;
using IntegratoR.OData.FO.Domain.Enums.General;
using IntegratoR.OData.FO.Domain.Enums.LedgerJournals;

namespace IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

/// <summary>
/// Represents a single line within a general journal in D365 F&amp;O, mapping to the <c>LedgerJournalLine</c> data entity, and records a debit or credit against an account.
/// </summary>
[Table("LedgerJournalLines")]
public class LedgerJournalLine : BaseEntity
{
    /// <summary>
    /// Gets or sets the legal entity (company) in which the journal line is created. Part of the composite key.
    /// </summary>
    [Key]
    [ODataField(IsRequired = true)]
    [JsonPropertyName("dataAreaId")]
    public required string DataAreaId { get; set; }

    /// <summary>
    /// Gets or sets the journal batch number of the header to which this line belongs. Part of the composite key.
    /// </summary>
    [Key]
    [ODataField(IsRequired = true)]
    [JsonPropertyName("JournalBatchNumber")]
    public required string JournalBatchNumber { get; set; }

    /// <summary>
    /// Gets or sets the system-generated line number that sequences each line within a journal, assigned by D365 F&amp;O on create. Part of the composite key.
    /// </summary>
    [Key]
    [JsonPropertyName("LineNumber")]
    [ODataField(IgnoreOnCreate = true)]
    public decimal LineNumber { get; set; }

    /// <summary>
    /// Gets or sets the primary account for the transaction, combining the main account and financial dimensions.
    /// </summary>
    [JsonPropertyName("AccountDisplayValue")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual required string AccountDisplayValue { get; set; }

    /// <summary>
    /// Gets or sets the type of the primary account, which determines the business logic applied during posting.
    /// </summary>
    [JsonPropertyName("AccountType")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual LedgerJournalACType AccountType { get; set; }

    /// <summary>
    /// Gets or sets the debit amount in the transaction currency. Must be zero if <see cref="CreditAmount"/> has a value.
    /// </summary>
    [ODataField(IsRequired = true)]
    [JsonPropertyName("DebitAmount")]
    public virtual decimal DebitAmount { get; set; }

    /// <summary>
    /// Gets or sets the credit amount in the transaction currency. Must be zero if <see cref="DebitAmount"/> has a value.
    /// </summary>
    [ODataField(IsRequired = true)]
    [JsonPropertyName("CreditAmount")]
    public virtual decimal CreditAmount { get; set; }

    /// <summary>
    /// Gets or sets the three-letter ISO code for the currency of the transaction.
    /// </summary>
    [ODataField(IsRequired = true)]
    [JsonPropertyName("CurrencyCode")]
    public virtual required string CurrencyCode { get; set; }

    /// <summary>
    /// Gets or sets the offsetting account for the transaction, combining the main account and financial dimensions.
    /// </summary>
    [JsonPropertyName("OffsetAccountDisplayValue")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? OffsetAccountDisplayValue { get; set; }

    /// <summary>
    /// Gets or sets the type of the offsetting account.
    /// </summary>
    [JsonPropertyName("OffsetAccountType")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual LedgerJournalACType OffsetAccountType { get; set; }

    /// <summary>
    /// Gets or sets the legal entity (company) of the offsetting account, required for intercompany transactions.
    /// </summary>
    [JsonPropertyName("OffsetCompany")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? OffsetCompany { get; set; }

    /// <summary>
    /// Gets or sets the transaction date, which determines the posting date for the financial entry.
    /// </summary>
    [JsonPropertyName("TransDate")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual required DateTimeOffset TransDate { get; set; }

    /// <summary>
    /// Gets or sets descriptive text for the transaction line, carried through to the general ledger.
    /// </summary>
    [JsonPropertyName("Text")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? TransactionText { get; set; }

    /// <summary>
    /// Gets or sets the voucher number, which can group multiple related journal lines. Assigned from the journal's number sequence when not provided.
    /// </summary>
    [JsonPropertyName("Voucher")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? Voucher { get; set; }

    /// <summary>
    /// Gets or sets the financial dimensions associated with the main account.
    /// </summary>
    [JsonPropertyName("DefaultDimensionDisplayValue")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? DefaultDimensionDisplayValue { get; set; }

    /// <summary>
    /// Gets or sets the financial dimensions associated with the offset account.
    /// </summary>
    [JsonPropertyName("OffsetDefaultDimensionDisplayValue")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? OffsetDefaultDimensionDisplayValue { get; set; }

    /// <summary>
    /// Gets or sets the due date for the transaction, used for customer and vendor account types to calculate payment terms.
    /// </summary>
    [JsonPropertyName("DueDate")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual DateTimeOffset DueDate { get; set; }

    /// <summary>
    /// Gets or sets the document date, often the date on an external document such as a vendor invoice.
    /// </summary>
    [JsonPropertyName("DocumentDate")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual DateTimeOffset DocumentDate { get; set; }

    /// <summary>
    /// Gets or sets a reference to an external document, such as a vendor invoice number.
    /// </summary>
    [JsonPropertyName("Document")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? Document { get; set; }

    /// <summary>
    /// Gets or sets the customer or vendor invoice number associated with the transaction.
    /// </summary>
    [JsonPropertyName("Invoice")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? Invoice { get; set; }

    /// <summary>
    /// Gets or sets the posting profile for customer or vendor transactions, which controls the summary accounts for posting.
    /// </summary>
    [JsonPropertyName("PostingProfile")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? PostingProfile { get; set; }

    /// <summary>
    /// Gets or sets the method of payment for the transaction.
    /// </summary>
    [JsonPropertyName("PaymentMethod")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? PaymentMethod { get; set; }

    /// <summary>
    /// Gets or sets a payment reference or note, often used for electronic fund transfers.
    /// </summary>
    [JsonPropertyName("PaymentReference")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? PaymentReference { get; set; }

    /// <summary>
    /// Gets or sets the exchange rate between the transaction currency and the accounting currency.
    /// </summary>
    [JsonPropertyName("ExchRate")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual decimal ExchRate { get; set; }

    /// <summary>
    /// Gets or sets the sales tax group associated with the transaction, which helps determine applicable taxes.
    /// </summary>
    [JsonPropertyName("SalesTaxGroup")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? SalesTaxGroup { get; set; }

    /// <summary>
    /// Gets or sets the item sales tax group, combined with <see cref="SalesTaxGroup"/> to determine the specific sales tax code.
    /// </summary>
    [JsonPropertyName("ItemSalesTaxGroup")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? ItemSalesTaxGroup { get; set; }

    /// <summary>
    /// Gets or sets the specific sales tax code applied to the transaction line.
    /// </summary>
    [JsonPropertyName("SalesTaxCode")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? SalesTaxCode { get; set; }

    /// <summary>
    /// Gets or sets the tax-exempt number, used if the transaction is exempt from sales tax.
    /// </summary>
    [JsonPropertyName("TaxExemptNumber")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual string? TaxExemptNumber { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this line is a reversing entry.
    /// </summary>
    [JsonPropertyName("ReverseEntry")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual NoYes ReverseEntry { get; set; }

    /// <summary>
    /// Gets or sets the date on which the reversing entry is posted. Mandatory when <see cref="ReverseEntry"/> is <see cref="NoYes.Yes"/>.
    /// </summary>
    [JsonPropertyName("ReverseDate")]
    [ODataField(IgnoreOnCreate = true)]
    public virtual DateTimeOffset ReverseDate { get; set; }

    /// <summary>
    /// Gets the composite key formed from <see cref="DataAreaId"/>, <see cref="JournalBatchNumber"/>, and <see cref="LineNumber"/>.
    /// </summary>
    public override object[] GetCompositeKey()
    {
        return [DataAreaId, JournalBatchNumber, LineNumber];
    }
}
