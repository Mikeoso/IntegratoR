# Create a Ledger Journal

Create a general journal header in D365 F&O and add transaction lines to it using the specialised ledger journal commands.

> **Prerequisites:** [[Configure-the-OData-Connection]], [[Register-Services-in-Your-Host]]

## Create the Journal Header

```csharp
using FluentResults;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.FO.Domain.Enums.LedgerJournals;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;
using MediatR;

// Build the header — JournalBatchNumber is server-generated, so leave it null.
var header = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalName = "GenJrn",
    Description = "Monthly accruals - March 2026"
};

var command = new CreateLedgerJournalHeaderCommand<LedgerJournalHeader>(header);
Result<LedgerJournalHeader> result = await mediator.Send(command, cancellationToken);

if (result.IsSuccess)
{
    LedgerJournalHeader created = result.Value;
    // JournalBatchNumber is now populated by F&O
    Console.WriteLine($"Created journal {created.JournalBatchNumber} in {created.DataAreaId}");
}
```

```text
Created journal 000234 in USMF
```

The `CreateLedgerJournalHeaderCommand<TEntity>` wraps the generic `CreateCommand<TEntity>` and adds domain-specific logging for `JournalName` and `DataAreaId`. The `ODataField(IgnoreOnCreate = true)` attribute on `JournalBatchNumber` ensures it is excluded from the create payload — F&O assigns it from the journal's number sequence.

## Add Lines to the Journal

Once you have the server-generated `JournalBatchNumber`, create lines against it:

```csharp
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;

var line = new LedgerJournalLine
{
    DataAreaId = "USMF",
    JournalBatchNumber = created.JournalBatchNumber!, // from header result
    AccountDisplayValue = "110180-001-027--",
    AccountType = LedgerJournalACType.Ledger,
    CurrencyCode = "USD",
    TransDate = new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero),
    DebitAmount = 1500.00m,
    CreditAmount = 0m
};

var lineCommand = new CreateLedgerJournalLineCommand<LedgerJournalLine>(line);
Result<LedgerJournalLine> lineResult = await mediator.Send(lineCommand, cancellationToken);

if (lineResult.IsSuccess)
{
    LedgerJournalLine createdLine = lineResult.Value;
    // LineNumber is server-generated
    Console.WriteLine($"Line {createdLine.LineNumber} added to journal {createdLine.JournalBatchNumber}");
}
```

```text
Line 1.0000 added to journal 000234
```

The `LineNumber` property has `ODataField(IgnoreOnCreate = true)`, so F&O assigns it automatically.

## Create Multiple Headers or Lines in Batch

Use the batch command variants to create multiple entities at once:

```csharp
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;

var headers = new List<LedgerJournalHeader>
{
    new() { DataAreaId = "USMF", JournalName = "GenJrn", Description = "Batch journal 1" },
    new() { DataAreaId = "USMF", JournalName = "GenJrn", Description = "Batch journal 2" }
};

var batchCommand = new CreateLedgerJournalHeadersCommand<LedgerJournalHeader>(headers);
Result<IEnumerable<LedgerJournalHeader>> batchResult = await mediator.Send(batchCommand, cancellationToken);
// Result: Result<IEnumerable<LedgerJournalHeader>> — all created headers with server-generated batch numbers
```

## When Things Go Wrong

If you omit a required field like `JournalName`, the pipeline validation catches it before it reaches F&O:

```csharp
var invalidHeader = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalName = null!, // required field
    Description = "Missing journal name"
};

var command = new CreateLedgerJournalHeaderCommand<LedgerJournalHeader>(invalidHeader);
Result<LedgerJournalHeader> result = await mediator.Send(command, cancellationToken);

// result.IsFailed == true
// result.Errors contains validation error for JournalName
```

If you reference a `JournalBatchNumber` that does not exist when creating a line, F&O returns an OData error which is surfaced through `Result.Fail`:

```csharp
var orphanLine = new LedgerJournalLine
{
    DataAreaId = "USMF",
    JournalBatchNumber = "NONEXISTENT",
    AccountDisplayValue = "110180-001-027--",
    AccountType = LedgerJournalACType.Ledger,
    CurrencyCode = "USD",
    TransDate = DateTimeOffset.UtcNow,
    DebitAmount = 100m
};

Result<LedgerJournalLine> result = await mediator.Send(
    new CreateLedgerJournalLineCommand<LedgerJournalLine>(orphanLine), cancellationToken);

// result.IsFailed == true — F&O rejected the request
```

## See Also

- [[Create-an-Entity]] — generic create pattern
- [[Build-Financial-Dimension-Strings]] — construct `AccountDisplayValue` dimension strings
- [[Write-a-Specialized-Command]] — how the ledger journal commands extend the generic ones
- [[Query-Dimension-Formats]] — fetch dimension format from F&O before building dimension strings
