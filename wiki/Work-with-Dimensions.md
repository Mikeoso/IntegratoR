# Work with Dimensions
> Last verified against v2.0.1

D365 F&O encodes a financial dimension as a delimiter-separated string like `618160-001-PC42`. The segment order is per-environment metadata, so resolve the format from D365 first, then use `FinancialDimensionBuilder` to write a value and `FinancialDimensionReader` to read one back.

```csharp
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Builders;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;
using IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;

// 1. Resolve the active format for company USMF (cached for 15 minutes).
Result<DimensionFormat> formatResult = await mediator.Send(
    new GetDimensionOrdersQuery(
        DimensionFormat: "Sachkontodimensionen",
        HierarchyType: DimensionHierarchyType.DataEntityLedgerDimensionFormat),
    cancellationToken).ConfigureAwait(false);

if (formatResult.IsFailed)
{
    IntegrationError? error = formatResult.GetError();
    // error.Message names the missing metadata; fail explicitly rather than guessing.
    return;
}

DimensionFormat format = formatResult.Value;
// format.Delimiter == "-"
// format.Segments  == ["MainAccount", "A_Kostenstelle", "C_Profitcenter"]

// 2. Build a display value; the Add order is irrelevant — Build sorts by format.Segments.
string displayValue = new FinancialDimensionBuilder()
    .Initialize(format)
    .Add("C_Profitcenter", "PC42")
    .Add("MainAccount", "618160")
    .Add("A_Kostenstelle", "001")
    .Build();

// displayValue == "618160-001-PC42"
```

`GetDimensionOrdersQuery(string DimensionFormat, DimensionHierarchyType HierarchyType)` takes PascalCase parameters (since v2.0.0 — the old camelCase `dimensionFormat`/`hierarchyType` are gone). `DimensionFormat` matches D365's `DimensionFormatName`; `HierarchyType` matches `DimensionFormatType`. The handler chains two reads — the active `DimensionIntegrationFormat` row, then the singleton `DimensionParameters` row for the delimiter — and splits the format string into ordered `Segments`.

## Handle the failure path

`GetDimensionOrdersQuery` returns a failed `Result<DimensionFormat>` when the delimiter row is missing:

```csharp
Result<DimensionFormat> result = await mediator.Send(
    new GetDimensionOrdersQuery("Sachkontodimensionen",
        DimensionHierarchyType.DataEntityLedgerDimensionFormat),
    cancellationToken).ConfigureAwait(false);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    if (error?.Code == "DimensionParameters.NotFound" && error.Type == ErrorType.NotFound)
    {
        // The singleton DimensionParameters row was never seeded in this environment.
        // Seed it in D365 (General ledger > Setup) before retrying.
    }
    return;
}
```

The handler propagates the underlying OData errors verbatim on a lookup failure (entity-set-not-found, authentication, APIM rejection), so `error.Message` carries the real cause rather than a generic wrapper.

> [!CAUTION]
> `GetDimensionOrdersQuery` **succeeds with an empty `Segments` list** when the format name matches zero rows — it is not a `NotFound` failure. Treat an empty `Segments` as a misspelled `DimensionFormat` or missing D365 setup, and check it before building.

## Read a dimension string back

`FinancialDimensionReader.Parse(DimensionFormat, string)` is the inverse of the builder. It returns `Result<Dictionary<string, string>>` mapping each segment name to its value, preserving empty segments for lossless round-tripping:

```csharp
Result<Dictionary<string, string>> parsed =
    FinancialDimensionReader.Parse(format, "618160-001-PC42");

if (parsed.IsFailed)
{
    IntegrationError? error = parsed.GetError();
    // error.Code is one of DimensionReader.InvalidFormat / EmptyInput / SegmentCountMismatch,
    // all ErrorType.Validation. SegmentCountMismatch is the common one — the string was built
    // with a different format than the one passed here.
    return;
}

Dictionary<string, string> segments = parsed.Value;
// segments["MainAccount"]    == "618160"
// segments["A_Kostenstelle"] == "001"
// segments["C_Profitcenter"] == "PC42"
```

The three failure codes, all `ErrorType.Validation`:

| Code | Cause |
|---|---|
| `DimensionReader.InvalidFormat` | `format` is null, has no segments, has an empty delimiter, or a segment name is empty. |
| `DimensionReader.EmptyInput` | `dimensionString` is null or empty. |
| `DimensionReader.SegmentCountMismatch` | The string splits into a different segment count than `format.Segments` — usually a format mismatch between build and read. |

## How Build handles omitted segments

`Build` walks `format.Segments` in order and emits an empty placeholder for any segment you did not `Add`:

```
Segments:    MainAccount  →  A_Kostenstelle  →  C_Profitcenter
Added:       "618160"        (none)             "PC42"
Joined:      "618160"  "-"  ""  "-"  "PC42"   ==  "618160--PC42"
```

The double delimiter `--` is the D365 convention for an intentionally blank segment; dropping it would shift `C_Profitcenter` into the `A_Kostenstelle` slot when D365 parses the string. `Add` ignores a null, empty, or whitespace `name` or `value` (a no-op), and `Build` returns an empty string when the builder was never initialised. Call `Initialize(format)` again or `Clear()` to reuse the instance.

## Configure a default format name

Bind `FOSettings` to carry an environment default so you do not repeat the name on every call:

```json
{
  "FOSettings": {
    "DimensionFormatName": "Sachkontodimensionen",
    "DimensionHierarchyType": "DataEntityLedgerDimensionFormat"
  }
}
```

Read it via `IOptions<FOSettings>`, or override it programmatically through the builder hook — `AddIntegratoR` is the only DI entry point:

```csharp
services.AddIntegratoR(configuration, integrator =>
{
    integrator.ConfigureFO(fo =>
    {
        fo.DimensionFormatName = "Sachkontodimensionen";
        fo.DimensionHierarchyType = DimensionHierarchyType.DataEntityLedgerDimensionFormat;
    });
});
```

## DON'T / DO — persisting the value on a journal line

> [!WARNING]
> `DefaultDimensionDisplayValue` on `LedgerJournalLine` is `[ODataField(IgnoreOnCreate = true)]` — it is stripped from the create payload. Set it on a `CreateCommand<LedgerJournalLine>` and the create appears to succeed while the dimensions silently vanish.

```csharp
// DON'T — the dimension string is dropped on create; the line posts without dimensions.
LedgerJournalLine line = new()
{
    DataAreaId = "USMF",
    JournalBatchNumber = "B0001",
    AccountDisplayValue = "618160",
    AccountType = LedgerJournalACType.Ledger,
    DebitAmount = 1000m,
    CreditAmount = 0m,
    CurrencyCode = "EUR",
    TransDate = DateTimeOffset.UtcNow,
    DefaultDimensionDisplayValue = displayValue // stripped — IgnoreOnCreate
};

// DO — carry the dimensions inside AccountDisplayValue, the account+dimension composite string.
line.AccountDisplayValue = displayValue; // e.g. "618160-001-PC42"
```

`AccountDisplayValue` is **also** `[ODataField(IgnoreOnCreate = true)]`, so the framework strips it from the POST body too. D365 resolves the main account and dimensions server-side from the journal template; whether it accepts the composite string on create depends on that template, so it is not a guaranteed round-trip. Read the value back from `result.Value` after the create, and see [Known Limitations](Known-Limitations) for the `required` + `IgnoreOnCreate` audit.

`GetCharValue` currently maps only the `Hyphen` delimiter; any other `DimensionSegmentDelimiter` throws `ArgumentOutOfRangeException` inside the handler. If your environment uses a non-hyphen delimiter, that is a live gap — see [Known Limitations](Known-Limitations).

The bundled smoke test proves this flow against a real D365 sandbox — `POST /api/smoke/financial-dimensions` runs `GetDimensionOrdersQuery` end to end and returns the live delimiter and segments. See [Run Smoke Tests](Run-Smoke-Tests).

## See Also
- [Run Queries](Run-Queries)
- [Cache Query Results](Cache-Query-Results)
- [Handle Errors](Handle-Errors)
- [Run Smoke Tests](Run-Smoke-Tests)
