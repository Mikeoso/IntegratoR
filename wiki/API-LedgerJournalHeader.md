# LedgerJournalHeader

D365 F&O entity representing a general journal header. Maps to the `LedgerJournalHeaders` OData entity set and the underlying `LedgerJournalTable` table.

## Use the Entity

```csharp
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

var header = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalName = "GenJrn",
    Description = "Monthly accruals - March 2026"
};

Result<LedgerJournalHeader> result = await service.AddAsync(header, cancellationToken);

if (result.IsSuccess)
{
    string batchNumber = result.Value.JournalBatchNumber!;
    // "JRN-000042" (server-generated)
}
```

## Properties

| Property | Type | Key | Required | ODataField | JSON Name | Description |
|----------|------|-----|----------|------------|-----------|-------------|
| `DataAreaId` | `string` | Yes | Yes | -- | `dataAreaId` | Legal entity / company code |
| `JournalBatchNumber` | `string?` | Yes | No | `IgnoreOnCreate` | `JournalBatchNumber` | Server-generated batch number |
| `JournalName` | `string` | No | Yes | -- | `JournalName` | Journal name setup identifier (e.g. "GenJrn") |
| `Description` | `string` | No | Yes | -- | `Description` | User-defined description |
| `IntegrationKey` | `string?` | No | No | -- | `IntegrationKey` | External system tracking key for idempotency |
| `PostingLayer` | `CurrentOperationsTax` | No | No | -- | `PostingLayer` | Financial posting layer (Current, Operations, Tax) |
| `IsPosted` | `NoYes` | No | No | -- | `IsPosted` | Read-only flag: whether the journal has been posted |
| `JournalTotalDebit` | `decimal` | No | Yes | -- | `JournalTotalDebit` | Read-only: sum of all line debit amounts |
| `JournalTotalCredit` | `decimal` | No | Yes | -- | `JournalTotalCredit` | Read-only: sum of all line credit amounts |
| `AccountingCurrency` | `string?` | No | No | -- | `AccountingCurrency` | Read-only: legal entity's base currency |

## Composite Key

`LedgerJournalHeader` uses a two-part composite key:

```csharp
public override object[] GetCompositeKey()
{
    return [DataAreaId, JournalBatchNumber ?? "null"];
}
```

This maps to the OData key format:
```
/data/LedgerJournalHeaders(dataAreaId='USMF',JournalBatchNumber='JRN-000042')
```

## See Examples

### Create a Journal Header

```csharp
var header = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalName = "GenJrn",
    Description = "Vendor invoice accruals",
    IntegrationKey = "EXT-2026-03-001"
};

Result<LedgerJournalHeader> result = await service.AddAsync(header, cancellationToken);
```

The POST payload excludes `JournalBatchNumber` (IgnoreOnCreate):
```json
{
  "dataAreaId": "USMF",
  "JournalName": "GenJrn",
  "Description": "Vendor invoice accruals",
  "IntegrationKey": "EXT-2026-03-001"
}
```

### Query Unposted Journals

```csharp
Result<IEnumerable<LedgerJournalHeader>> result = await service.QueryAsync(
    filter: h => h.DataAreaId == "USMF" && h.IsPosted == NoYes.No,
    top: 50,
    cancellationToken: cancellationToken);

if (result.IsSuccess)
{
    foreach (LedgerJournalHeader header in result.Value)
    {
        Console.WriteLine($"{header.JournalBatchNumber}: {header.Description}");
    }
}
```

### Retrieve by Key

```csharp
Result<LedgerJournalHeader> result = await service.GetByKeyAsync(
    ["USMF", "JRN-000042"], cancellationToken);

if (result.IsFailed)
{
    // ErrorType.NotFound if journal does not exist
}
```

### Error Handling

```csharp
// Missing required field
var header = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalName = "GenJrn",
    Description = "Test"
};

// If JournalName references a non-existent setup
Result<LedgerJournalHeader> result = await service.AddAsync(header, cancellationToken);

if (result.IsFailed)
{
    // D365 returns 400 Bad Request
    // IntegrationError with ErrorType.Failure
}
```

## Entity Metadata

```csharp
[Table("LedgerJournalHeaders")]
public class LedgerJournalHeader : BaseEntity<string>
```

- **Entity set:** `LedgerJournalHeaders`
- **Base class:** `BaseEntity<string>`
- **Namespace:** `IntegratoR.OData.FO.Domain.Entities.LedgerJournal`

## See Also

- [[API-LedgerJournalLine]] — journal line entity (child of this header)
- [[API-ODataFieldAttribute]] — controls payload serialisation
- [[Create-a-Ledger-Journal]] — end-to-end journal creation guide
