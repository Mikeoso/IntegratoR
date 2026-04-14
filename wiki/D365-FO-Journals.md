# D365 F&O Journals

```csharp
// Create a journal header, build dimension strings, and add lines
LedgerJournalHeader header = new()
{
    DataAreaId = "USMF", JournalName = "GenJrn", Description = "Monthly accruals"
};
Result<LedgerJournalHeader> headerResult = await mediator
    .Send(new CreateCommand<LedgerJournalHeader>(header), cancellationToken)
    .ConfigureAwait(false);

if (headerResult.IsFailed) return; // handle error
string batchNumber = headerResult.Value.JournalBatchNumber!; // server-generated, e.g. "000234"
```

The `IntegratoR.SampleFunction` project demonstrates this flow end-to-end in
`LedgerJournalSmokeTestTrigger` (`POST /api/smoke/ledger-journal`): create header, get by
key, filter by `dataAreaId`, create a balanced debit/credit line pair, update, and
best-effort cleanup — all through the generic `CreateCommand<T>`, `UpdateCommand<T>`,
`DeleteCommand<T>`, `GetByKeyQuery<T>`, and `GetByFilterQuery<T>` dispatched via MediatR.

## LedgerJournalHeader

`LedgerJournalHeader : BaseEntity<string>` maps to the `LedgerJournalHeaders` OData entity set (`LedgerJournalTable` table). Composite key: `DataAreaId` + `JournalBatchNumber`.

```csharp
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;

LedgerJournalHeader header = new()
{
    DataAreaId = "USMF",
    JournalName = "GenJrn",
    Description = "Vendor invoice accruals",
    IntegrationKey = "EXT-2026-03-001" // optional idempotency key
};

Result<LedgerJournalHeader> result = await mediator
    .Send(new CreateCommand<LedgerJournalHeader>(header), cancellationToken)
    .ConfigureAwait(false);
// JournalBatchNumber excluded from POST (ODataField IgnoreOnCreate) — F&O assigns it
```

| Property | Type | Key | ODataField | Description |
|----------|------|-----|------------|-------------|
| `DataAreaId` | `string` | Yes | -- | Legal entity / company code |
| `JournalBatchNumber` | `string?` | Yes | `IgnoreOnCreate` | Server-generated batch number |
| `JournalName` | `string` | No | -- | Journal name setup identifier (e.g. "GenJrn") |
| `Description` | `string` | No | -- | User-defined description |
| `IntegrationKey` | `string?` | No | -- | External system tracking key |
| `PostingLayer` | `CurrentOperationsTax` | No | -- | Financial posting layer |
| `IsPosted` | `NoYes` | No | -- | Server-managed: whether posted (not stripped from payload, but D365 ignores client values) |
| `JournalTotalDebit` | `decimal` | No | -- | Server-calculated: sum of line debits (not stripped from payload, but D365 ignores client values) |
| `JournalTotalCredit` | `decimal` | No | -- | Server-calculated: sum of line credits (not stripped from payload, but D365 ignores client values) |

Batch creation uses `CreateBatchCommand<LedgerJournalHeader>(headers)` returning `Result` (non-generic — batch commands do not return created entities). See [[Batch-Operations]].

## LedgerJournalLine

`LedgerJournalLine : BaseEntity<string>` maps to `LedgerJournalLines` (`LedgerJournalTrans` table). Composite key: `DataAreaId` + `JournalBatchNumber` + `LineNumber`.

```csharp
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.FO.Domain.Enums.LedgerJournals;

LedgerJournalLine debitLine = new()
{
    DataAreaId = "USMF",
    JournalBatchNumber = batchNumber,
    AccountDisplayValue = "110110-001-025",  // required by C#, excluded from POST payload
    AccountType = LedgerJournalACType.Ledger, // required by C#, excluded from POST payload
    CurrencyCode = "USD",                     // required by C#, excluded from POST payload
    DebitAmount = 5000.00m,
    TransDate = new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero), // required by C#, excluded from POST payload
    TransactionText = "Rent expense"          // excluded from POST payload
};

LedgerJournalLine creditLine = new()
{
    DataAreaId = "USMF",
    JournalBatchNumber = batchNumber,
    AccountDisplayValue = "200110-001-025",   // required by C#, excluded from POST payload
    AccountType = LedgerJournalACType.Ledger, // required by C#, excluded from POST payload
    CurrencyCode = "USD",                     // required by C#, excluded from POST payload
    CreditAmount = 5000.00m,
    TransDate = new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero), // required by C#, excluded from POST payload
    TransactionText = "Rent accrual"          // excluded from POST payload
};

Result<LedgerJournalLine> debitResult = await mediator
    .Send(new CreateCommand<LedgerJournalLine>(debitLine), cancellationToken)
    .ConfigureAwait(false);
// LineNumber is server-generated (IgnoreOnCreate), e.g. 1.0000
```

Most properties have `[ODataField(IgnoreOnCreate = true)]` — D365 F&O populates them from journal name defaults. Set values on the C# object for use after creation (e.g. logging), but they are excluded from the POST payload. Only `DataAreaId`, `JournalBatchNumber`, `DebitAmount`, and `CreditAmount` are included in the POST.

| Property | Type | ODataField | Description |
|----------|------|------------|-------------|
| `DataAreaId` | `string` | -- | Legal entity (key) |
| `JournalBatchNumber` | `string` | -- | Parent header (key) |
| `LineNumber` | `decimal` | `IgnoreOnCreate` | Server-generated (key) |
| `AccountDisplayValue` | `string` | `IgnoreOnCreate` | Primary account + dimensions |
| `AccountType` | `LedgerJournalACType` | `IgnoreOnCreate` | Ledger, Customer, Vendor, etc. |
| `DebitAmount` / `CreditAmount` | `decimal` | -- | Transaction amounts (included in POST) |
| `CurrencyCode` | `string` | `IgnoreOnCreate` | ISO currency code |
| `TransDate` | `DateTimeOffset` | `IgnoreOnCreate` | Transaction date |
| `OffsetAccountDisplayValue` | `string?` | `IgnoreOnCreate` | Offset account + dimensions |
| `OffsetAccountType` | `LedgerJournalACType` | `IgnoreOnCreate` | Offset account type |
| `DefaultDimensionDisplayValue` | `string?` | `IgnoreOnCreate` | Default dimensions on primary account |
| `TransactionText` | `string?` | `IgnoreOnCreate` | Text carried to general ledger |
| `Voucher` | `string?` | `IgnoreOnCreate` | Auto-assigned if omitted |
| `SalesTaxGroup` / `ItemSalesTaxGroup` | `string?` | `IgnoreOnCreate` | Tax groups |
| `ReverseEntry` | `NoYes` | `IgnoreOnCreate` | Reversing entry flag |
| `ReverseDate` | `DateTimeOffset` | `IgnoreOnCreate` | Required if `ReverseEntry = Yes` |

## FinancialDimensionBuilder

`FinancialDimensionBuilder` (`IntegratoR.OData.FO.Builders`) constructs dimension strings in the segment order F&O expects, regardless of the order you call `Add`.

```csharp
using IntegratoR.OData.FO.Builders;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;

DimensionFormat format = new() { Delimiter = "-", Segments = ["MainAccount", "BusinessUnit", "CostCenter"] };

string account = new FinancialDimensionBuilder()
    .Initialize(format)             // Initialize(DimensionFormat) -> FinancialDimensionBuilder
    .Add("MainAccount", "110110")   // Add(string name, string value) -> FinancialDimensionBuilder
    .Add("CostCenter", "025")
    .Add("BusinessUnit", "001")
    .Build();                       // Build() -> string
// account == "110110-001-025" (ordered by format, not by Add calls)
```

Missing segments produce empty placeholders: `builder.Add("CostCenter", "CC002").Build()` with the format above yields `"--CC002"`. Null/whitespace names or values are silently ignored. Calling `Build()` without `Initialize` returns `""`.

`Initialize` clears previous state, so the builder is reusable without calling `Clear()` explicitly.

## Querying Dimension Formats

Rather than hardcoding `DimensionFormat`, fetch it from F&O with GetDimensionOrdersQuery. Results are cached for 15 minutes via `ICacheableQuery`.

```csharp
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;

GetDimensionOrdersQuery query = new(
    "LedgerDimension",
    DimensionHierarchyType.DataEntityLedgerDimensionFormat);

Result<DimensionFormat> formatResult = await mediator
    .Send(query, cancellationToken)
    .ConfigureAwait(false);
// CacheKey: "GetDimensionOrdersQuery-LedgerDimension-DataEntityLedgerDimensionFormat"

string accountDisplayValue = new FinancialDimensionBuilder()
    .Initialize(formatResult.Value)  // e.g. Delimiter="-", Segments=["MainAccount","BusinessUnit","Department","CostCenter"]
    .Add("MainAccount", "110110")
    .Add("BusinessUnit", "001")
    .Add("CostCenter", "025")
    .Build(); // "110110-001--025"
```

| Hierarchy Type | Use Case |
|---|---|
| `DataEntityDefaultDimensionFormat` | Default dimensions (without main account) |
| `DataEntityLedgerDimensionFormat` | Ledger dimensions (main account + dimensions) |
| `AccountStructure` | Chart of accounts structure |
| `DataEntityBudgetDimensionFormat` | Budget dimensions |

## End-to-End: Journal with Dimensions

```csharp
// 1. Fetch dimension format from F&O
Result<DimensionFormat> format = await mediator
    .Send(new GetDimensionOrdersQuery("LedgerDimension",
        DimensionHierarchyType.DataEntityLedgerDimensionFormat), cancellationToken)
    .ConfigureAwait(false);

FinancialDimensionBuilder dimBuilder = new();

// 2. Create journal header
LedgerJournalHeader header = new()
{
    DataAreaId = "USMF", JournalName = "GenJrn", Description = "March accruals"
};
Result<LedgerJournalHeader> headerResult = await mediator
    .Send(new CreateCommand<LedgerJournalHeader>(header), cancellationToken)
    .ConfigureAwait(false);

if (headerResult.IsFailed) return; // handle error
string journalId = headerResult.Value.JournalBatchNumber!;

// 3. Build account strings and create lines
string debitAccount = dimBuilder
    .Initialize(format.Value)
    .Add("MainAccount", "110180")
    .Add("BusinessUnit", "001")
    .Add("CostCenter", "027")
    .Build(); // e.g. "110180-001--027"

string creditAccount = dimBuilder
    .Initialize(format.Value)
    .Add("MainAccount", "200110")
    .Add("BusinessUnit", "001")
    .Build(); // e.g. "200110-001--"

List<LedgerJournalLine> lines =
[
    new()
    {
        DataAreaId = "USMF", JournalBatchNumber = journalId,
        AccountDisplayValue = debitAccount,                                    // required by C#, excluded from POST payload
        AccountType = LedgerJournalACType.Ledger,                              // required by C#, excluded from POST payload
        CurrencyCode = "USD", DebitAmount = 1500.00m,                          // CurrencyCode: excluded from POST payload
        TransDate = new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero)   // required by C#, excluded from POST payload
    },
    new()
    {
        DataAreaId = "USMF", JournalBatchNumber = journalId,
        AccountDisplayValue = creditAccount,                                   // required by C#, excluded from POST payload
        AccountType = LedgerJournalACType.Ledger,                              // required by C#, excluded from POST payload
        CurrencyCode = "USD", CreditAmount = 1500.00m,                         // CurrencyCode: excluded from POST payload
        TransDate = new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero)   // required by C#, excluded from POST payload
    }
];

Result lineResults = await mediator
    .Send(new CreateBatchCommand<LedgerJournalLine>(lines), cancellationToken)
    .ConfigureAwait(false);
```

## See Also

- [[Entities]] — `BaseEntity<TKey>` and `ODataFieldAttribute` reference
- [[Commands]] — generic CRUD commands
- [[Batch-Operations]] — bulk operations and chunking
- [[Configuration]] — `FOSettings` and dimension hierarchy types
