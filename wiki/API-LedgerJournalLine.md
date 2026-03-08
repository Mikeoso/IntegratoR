# LedgerJournalLine

D365 F&O entity representing a single line within a general journal. Maps to the `LedgerJournalLines` OData entity set and the underlying `LedgerJournalTrans` table.

## Use the Entity

```csharp
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

var line = new LedgerJournalLine
{
    DataAreaId = "USMF",
    JournalBatchNumber = "JRN-000042",
    AccountDisplayValue = "110110-001-025",
    AccountType = LedgerJournalACType.Ledger,
    CurrencyCode = "USD",
    DebitAmount = 5000.00m,
    TransDate = DateTimeOffset.Now,
    TransactionText = "Monthly rent accrual"
};

Result<LedgerJournalLine> result = await service.AddAsync(line, cancellationToken);
```

## Properties

### Key Properties

| Property | Type | Key | ODataField | JSON Name | Description |
|----------|------|-----|------------|-----------|-------------|
| `DataAreaId` | `string` | Yes | -- | `dataAreaId` | Legal entity / company code |
| `JournalBatchNumber` | `string` | Yes | -- | `JournalBatchNumber` | Parent journal header batch number |
| `LineNumber` | `decimal` | Yes | `IgnoreOnCreate` | `LineNumber` | Server-generated line sequence number |

### Financial Properties

| Property | Type | ODataField | JSON Name | Description |
|----------|------|------------|-----------|-------------|
| `AccountDisplayValue` | `string` | `IgnoreOnCreate` | `AccountDisplayValue` | Primary account with dimensions (e.g. "110110-001-025") |
| `AccountType` | `LedgerJournalACType` | `IgnoreOnCreate` | `AccountType` | Account type (Ledger, Customer, Vendor, etc.) |
| `DebitAmount` | `decimal` | -- | `DebitAmount` | Debit amount in transaction currency |
| `CreditAmount` | `decimal` | -- | `CreditAmount` | Credit amount in transaction currency |
| `CurrencyCode` | `string` | `IgnoreOnCreate` | `CurrencyCode` | ISO currency code (e.g. "USD", "EUR") |
| `ExchRate` | `decimal` | `IgnoreOnCreate` | `ExchRate` | Exchange rate to accounting currency |

### Offset Account Properties

| Property | Type | ODataField | JSON Name | Description |
|----------|------|------------|-----------|-------------|
| `OffsetAccountDisplayValue` | `string?` | `IgnoreOnCreate` | `OffsetAccountDisplayValue` | Offset account with dimensions |
| `OffsetAccountType` | `LedgerJournalACType` | `IgnoreOnCreate` | `OffsetAccountType` | Offset account type |
| `OffsetCompany` | `string?` | `IgnoreOnCreate` | `OffsetCompany` | Offset legal entity (for intercompany) |

### Date Properties

| Property | Type | ODataField | JSON Name | Description |
|----------|------|------------|-----------|-------------|
| `TransDate` | `DateTimeOffset` | `IgnoreOnCreate` | `TransDate` | Transaction / posting date |
| `DueDate` | `DateTimeOffset` | `IgnoreOnCreate` | `DueDate` | Payment due date |
| `DocumentDate` | `DateTimeOffset` | `IgnoreOnCreate` | `DocumentDate` | External document date |

### Reference Properties

| Property | Type | ODataField | JSON Name | Description |
|----------|------|------------|-----------|-------------|
| `TransactionText` | `string?` | `IgnoreOnCreate` | `Text` | Descriptive text carried to the general ledger |
| `Voucher` | `string?` | `IgnoreOnCreate` | `Voucher` | Voucher number (auto-assigned if omitted) |
| `Document` | `string?` | `IgnoreOnCreate` | `Document` | External document reference |
| `Invoice` | `string?` | `IgnoreOnCreate` | `Invoice` | Customer/vendor invoice number |

### Dimension Properties

| Property | Type | ODataField | JSON Name | Description |
|----------|------|------------|-----------|-------------|
| `DefaultDimensionDisplayValue` | `string?` | `IgnoreOnCreate` | `DefaultDimensionDisplayValue` | Financial dimensions for primary account |
| `OffsetDefaultDimensionDisplayValue` | `string?` | `IgnoreOnCreate` | `OffsetDefaultDimensionDisplayValue` | Financial dimensions for offset account |

### Tax Properties

| Property | Type | ODataField | JSON Name | Description |
|----------|------|------------|-----------|-------------|
| `SalesTaxGroup` | `string?` | `IgnoreOnCreate` | `SalesTaxGroup` | Sales tax group |
| `ItemSalesTaxGroup` | `string?` | `IgnoreOnCreate` | `ItemSalesTaxGroup` | Item sales tax group |
| `SalesTaxCode` | `string?` | `IgnoreOnCreate` | `SalesTaxCode` | Specific tax code |
| `TaxExemptNumber` | `string?` | `IgnoreOnCreate` | `TaxExemptNumber` | Tax exemption number |

### Other Properties

| Property | Type | ODataField | JSON Name | Description |
|----------|------|------------|-----------|-------------|
| `PostingProfile` | `string?` | `IgnoreOnCreate` | `PostingProfile` | Posting profile for customer/vendor transactions |
| `PaymentMethod` | `string?` | `IgnoreOnCreate` | `PaymentMethod` | Payment method (CHECK, EFT, etc.) |
| `PaymentReference` | `string?` | `IgnoreOnCreate` | `PaymentReference` | Payment reference for electronic transfers |
| `ReverseEntry` | `NoYes` | `IgnoreOnCreate` | `ReverseEntry` | Whether this is a reversing entry |
| `ReverseDate` | `DateTimeOffset` | `IgnoreOnCreate` | `ReverseDate` | Date for reversing entry (required if ReverseEntry = Yes) |

## Composite Key

`LedgerJournalLine` uses a three-part composite key:

```csharp
public override object[] GetCompositeKey()
{
    return [DataAreaId, JournalBatchNumber, LineNumber];
}
```

OData key format:
```
/data/LedgerJournalLines(dataAreaId='USMF',JournalBatchNumber='JRN-000042',LineNumber=1.0)
```

## See Examples

### Create Lines for a Journal

Most properties have `IgnoreOnCreate = true`, meaning D365 F&O populates them from defaults. On create, only `DataAreaId`, `JournalBatchNumber`, `DebitAmount`, and `CreditAmount` are sent:

```csharp
var debitLine = new LedgerJournalLine
{
    DataAreaId = "USMF",
    JournalBatchNumber = "JRN-000042",
    AccountDisplayValue = "110110-001-025",
    AccountType = LedgerJournalACType.Ledger,
    CurrencyCode = "USD",
    DebitAmount = 5000.00m,
    TransDate = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
    TransactionText = "Rent expense"
};

var creditLine = new LedgerJournalLine
{
    DataAreaId = "USMF",
    JournalBatchNumber = "JRN-000042",
    AccountDisplayValue = "200110-001-025",
    AccountType = LedgerJournalACType.Ledger,
    CurrencyCode = "USD",
    CreditAmount = 5000.00m,
    TransDate = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
    TransactionText = "Rent accrual"
};

// Create individually
Result<LedgerJournalLine> debitResult = await service.AddAsync(debitLine, cancellationToken);
Result<LedgerJournalLine> creditResult = await service.AddAsync(creditLine, cancellationToken);

// Or create in batch (atomic)
Result batchResult = await batchService.AddBatchAsync([debitLine, creditLine], cancellationToken);
```

### Query Lines for a Journal

```csharp
Result<IEnumerable<LedgerJournalLine>> result = await service.FindAsync(
    l => l.DataAreaId == "USMF" && l.JournalBatchNumber == "JRN-000042", cancellationToken);

if (result.IsSuccess)
{
    decimal totalDebits = result.Value.Sum(l => l.DebitAmount);
    decimal totalCredits = result.Value.Sum(l => l.CreditAmount);
}
```

### Error Handling

```csharp
Result<LedgerJournalLine> result = await service.GetByKeyAsync(
    ["USMF", "JRN-000042", 99.0m], cancellationToken);

if (result.IsFailed)
{
    IntegrationError error = result.Errors.OfType<IntegrationError>().First();
    // error.Type == ErrorType.NotFound
}
```

## Entity Metadata

```csharp
[Table("LedgerJournalLines")]
public class LedgerJournalLine : BaseEntity<string>
```

- **Entity set:** `LedgerJournalLines`
- **Base class:** `BaseEntity<string>`
- **Namespace:** `IntegratoR.OData.FO.Domain.Entities.LedgerJournal`

## See Also

- [[API-LedgerJournalHeader]] — parent journal header entity
- [[API-FinancialDimensionBuilder]] — build dimension strings for account values
- [[Create-a-Ledger-Journal]] — end-to-end journal creation guide
