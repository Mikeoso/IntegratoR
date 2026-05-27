# Work with Dimensions

D365 F&O financial dimensions are encoded on the wire as delimited strings: `"618160-001-023-..."`. The position of each value is dictated by the per-environment **dimension format**, which the consumer reads from D365 via `GetDimensionOrdersQuery` and uses to drive the `FinancialDimensionBuilder` and `FinancialDimensionReader`.

## Read the Dimension Format from D365

```csharp
using FluentResults;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;
using IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;

Result<DimensionFormat> result = await mediator.Send(
    new GetDimensionOrdersQuery(
        dimensionFormat: "Sachkontodimensionen",
        hierarchyType: DimensionHierarchyType.DataEntityLedgerDimensionFormat),
    cancellationToken).ConfigureAwait(false);

if (result.IsSuccess)
{
    DimensionFormat format = result.Value;
    // format.Delimiter == "-"
    // format.Segments  == ["MainAccount", "A_Kostenstelle", "B_Segment", ...]
}
```

The query signature is `GetDimensionOrdersQuery(string dimensionFormat, DimensionHierarchyType hierarchyType)`. Both parameters are part of the entity's composite filter: `dimensionFormat` matches `DimensionIntegrationFormat.DimensionFormatName`, `hierarchyType` matches `DimensionIntegrationFormat.DimensionFormatType`.

The handler chains two calls to D365:

1. `DimensionIntegrationFormat.FindAsync` — filters on `DimensionFormatName == name && DimensionFormatType == type && IsActive == NoYes.Yes`. Returns the dimension format definition.
2. `DimensionParameters.FindAll` — returns the singleton row that carries the global `DimensionSegmentDelimiter` enum.

The handler then splits the `FinancialDimensionFormat` string from result (1) using the delimiter character resolved from result (2). The query is `ICacheableQuery` with a 15-minute cache duration — repeated calls within that window return the cached `DimensionFormat` instance.

## `DimensionHierarchyType` Values

The `DimensionHierarchyType` enum mirrors the D365 base enum. Most consumers need one of these:

| Value | Underlying int | Typical use |
|---|---|---|
| `AccountStructure` | 0 | Primary chart-of-accounts structure |
| `DataEntityDefaultDimensionFormat` | 17 | Default dimension format (no main account) |
| `DataEntityLedgerDimensionFormat` | 18 | **Ledger dimension format — main account + dimensions, most common** |
| `DataEntityBudgetDimensionFormat` | 19 | Budget dimension format |
| `Customer` | 7 | Dimensions attached to a Customer master record |
| `Vendor` | 8 | Dimensions attached to a Vendor master record |
| `Project` | 9 | Dimensions attached to a Project record |
| `FixedAsset` | 10 | Dimensions attached to a Fixed Asset record |
| `Employee` | 12 | Dimensions attached to an Employee record |

The full enum is in `IntegratoR.OData.FO.Domain.Enums.Dimensions.DimensionHierarchyType` — see the source for the complete list including country-specific variants.

## Build a Dimension String

```csharp
using IntegratoR.OData.FO.Builders;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;

DimensionFormat format = new()
{
    Delimiter = "-",
    Segments = new List<string> { "MainAccount", "Department", "CostCenter" }
};

FinancialDimensionBuilder builder = new();
string displayValue = builder
    .Initialize(format)
    .Add("CostCenter",  "CC002")
    .Add("MainAccount", "618160")
    .Build();

// displayValue == "618160--CC002"
```

The order of `Add(...)` calls does **not** matter. The builder sorts values into the segment order defined by `DimensionFormat.Segments`. A segment with no value contributes an empty placeholder between the delimiters — `"618160--CC002"` shows that the `Department` segment was intentionally blank, which D365 requires to maintain the structural integrity of the dimension string.

The `Add(...)` method ignores empty values silently: passing `null`, `""`, or whitespace for either `name` or `value` is a no-op. To remove a previously-added value, call `Initialize(format)` or `Clear()` and start over.

## How `Build()` Works

```
Segments:    MainAccount  →  Department  →  CostCenter
Added:       "618160"        (none)         "CC002"
Output part: "618160"        ""             "CC002"
Joined:      "618160" + "-" + "" + "-" + "CC002"
             = "618160--CC002"
```

The double delimiter `--` is the D365 convention for an intentionally-blank segment. Skipping the empty placeholder would shift `CostCenter` into the `Department` slot at parse time.

## Read a Dimension String Back

`FinancialDimensionReader` parses a delimited string back into a name → value dictionary using the same `DimensionFormat`. The reader is the inverse of the builder — the same `DimensionFormat` instance can drive both writes and reads.

## End-to-End Example

```csharp
// 1. Resolve the format from D365 (cached for 15 minutes).
Result<DimensionFormat> formatResult = await mediator.Send(
    new GetDimensionOrdersQuery(
        "Sachkontodimensionen",
        DimensionHierarchyType.DataEntityLedgerDimensionFormat),
    cancellationToken).ConfigureAwait(false);

if (formatResult.IsFailed)
{
    // Format does not exist in this environment — fail the operation explicitly.
    return Result.Fail<string>(formatResult.Errors);
}

// 2. Build a dimension display value for a journal line.
string displayValue = new FinancialDimensionBuilder()
    .Initialize(formatResult.Value)
    .Add("MainAccount",      "618160")
    .Add("A_Kostenstelle",   "001")
    .Add("C_Profitcenter",   "PC42")
    .Build();

// 3. Use it on a LedgerJournalLine, e.g. as DefaultDimensionDisplayValue.
LedgerJournalLine line = new()
{
    DataAreaId = "1210",
    JournalBatchNumber = "JBN-000431",
    DebitAmount = 1000m,
    CreditAmount = 0m,
    CurrencyCode = "EUR",
    // ... set the display value on the appropriate field
};
```

The live shape of this flow is captured by the bundled smoke-test trigger — see [Run Smoke Tests](Run-Smoke-Tests) for the HTTP call that exercises `GetDimensionOrdersQuery` end-to-end and shows the live response from a real D365 sandbox.

## Configure the Default Format Name

The `FOSettings` configuration section holds D365-specific defaults:

```json
{
  "FOSettings": {
    "DimensionFormatName": "Sachkontodimensionen"
  }
}
```

This is bound to `IntegratoR.OData.FO.Domain.Models.Settings.FOSettings` and is available via `IOptions<FOSettings>` for consumers that want a single configured default rather than passing the name on each call.

Programmatic overrides use the builder hook:

```csharp
services.AddIntegratoR(configuration, integrator =>
{
    integrator.ConfigureFO(fo =>
    {
        fo.DimensionFormatName = "Default account";
    });
});
```

## When Things Go Wrong

**`DimensionParameters.NotFound` with `ErrorType.NotFound`** — `DimensionParameters` is a singleton-row entity in D365. An empty response means the row was never seeded in this environment. Verify by browsing to the dimension parameters page in D365 F&O.

**Empty `Segments` returned successfully** — the dimension format name does not exist (filter matched zero rows). The handler returns an empty `DimensionFormat` with no segments rather than a `NotFound` failure in this case; an empty segment list at runtime indicates either a misspelled `dimensionFormat` parameter or a missing setup in D365.

**`DimensionFormat` returned but `Build()` produces a wildly wrong string** — the segments in D365 are not in the order the consumer expected. Always log the resolved `Segments` array at startup to catch this early.

## See Also

- [Run Smoke Tests](Run-Smoke-Tests) — the FinancialDimension smoke test exercises this flow live
- [Run Queries](Run-Queries) — `GetDimensionOrdersQuery` as a custom cacheable query
- [Cache Query Results](Cache-Query-Results) — the 15-minute cache duration governs how often D365 is hit
- [Define Entities](Define-Entities) — `DimensionIntegrationFormat` and `DimensionParameters` entity shapes
