# Query Dimension Formats

Fetch the financial dimension format from D365 F&O using `GetDimensionOrdersQuery`. The result tells you the segment order and delimiter needed by `FinancialDimensionBuilder`.

> **Prerequisites:** [[Configure-the-OData-Connection]], [[Register-Services-in-Your-Host]]

## Send the Query via MediatR

```csharp
using FluentResults;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;
using IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;
using MediatR;

var query = new GetDimensionOrdersQuery(
    "DefaultDimension",
    DimensionHierarchyType.DataEntityDefaultDimensionFormat);

Result<DimensionFormat> result = await mediator.Send(query, cancellationToken);

if (result.IsSuccess)
{
    DimensionFormat format = result.Value;
    Console.WriteLine($"Delimiter: {format.Delimiter}");
    Console.WriteLine($"Segments: {string.Join(", ", format.Segments)}");
}
```

```text
Delimiter: -
Segments: BusinessUnit, Department, CostCenter, Project
```

## Cache Results Automatically

`GetDimensionOrdersQuery` implements `ICacheableQuery<Result<DimensionFormat>>`, so the caching pipeline behaviour automatically caches the result for 15 minutes:

```csharp
// These properties are built into the query record:
// CacheKey:      "GetDimensionOrdersQuery-DefaultDimension-DataEntityDefaultDimensionFormat"
// CacheDuration: 15 minutes

// Second call within 15 minutes returns the cached result — no F&O round-trip.
Result<DimensionFormat> cachedResult = await mediator.Send(query, cancellationToken);
```

Different format names and hierarchy types produce different cache keys, so they are cached independently:

```csharp
// This query has a different cache key and is cached separately
var ledgerQuery = new GetDimensionOrdersQuery(
    "LedgerDimension",
    DimensionHierarchyType.DataEntityLedgerDimensionFormat);
```

## Feed the Result into FinancialDimensionBuilder

```csharp
using IntegratoR.OData.FO.Builders;

if (result.IsSuccess)
{
    string dimensionString = new FinancialDimensionBuilder()
        .Initialize(result.Value)
        .Add("BusinessUnit", "BU01")
        .Add("CostCenter", "CC002")
        .Build();

    Console.WriteLine(dimensionString);
}
```

```text
BU01---CC002
```

## Common Hierarchy Types

| Hierarchy Type | Use Case |
|---|---|
| `DataEntityDefaultDimensionFormat` | Default dimensions (without main account) on data entities |
| `DataEntityLedgerDimensionFormat` | Ledger dimensions (main account + dimensions) on data entities |
| `AccountStructure` | Chart of accounts structure |
| `DataEntityBudgetDimensionFormat` | Budget dimensions on data entities |

## When Things Go Wrong

If the dimension format name does not exist in F&O, or no active formats match, the query returns a failure:

```csharp
var query = new GetDimensionOrdersQuery(
    "NonExistentFormat",
    DimensionHierarchyType.DataEntityDefaultDimensionFormat);

Result<DimensionFormat> result = await mediator.Send(query, cancellationToken);

// result.IsFailed == true
// Error code: "DimensionParameters.QueryFailed"
// Error message: "No Data returned by the query"
```

If the dimension parameters table in F&O has no records (which would indicate a misconfigured environment), the query also fails with the same error code.

## See Also

- [[Build-Financial-Dimension-Strings]] — use the `DimensionFormat` to build dimension strings
- [[Create-a-Ledger-Journal]] — apply dimension strings to journal lines
- [[Configure-the-OData-Connection]] — set up the OData connection to F&O
