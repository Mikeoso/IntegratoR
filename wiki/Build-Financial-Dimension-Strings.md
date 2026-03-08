# Build Financial Dimension Strings

Construct correctly formatted financial dimension strings for D365 F&O using `FinancialDimensionBuilder`. The builder ensures dimension values appear in the order F&O expects, regardless of the order you add them.

> **Prerequisites:** [[Query-Dimension-Formats]]

## Initialise the Builder and Add Segments

```csharp
using IntegratoR.OData.FO.Builders;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;

// Define the dimension format (typically fetched from F&O via GetDimensionOrdersQuery).
var format = new DimensionFormat
{
    Delimiter = "-",
    Segments = new List<string> { "BusinessUnit", "Department", "CostCenter" }
};

var builder = new FinancialDimensionBuilder();

string displayValue = builder
    .Initialize(format)
    .Add("CostCenter", "CC002")
    .Add("BusinessUnit", "BU01")
    .Build();

Console.WriteLine(displayValue);
```

```text
BU01--CC002
```

Segments are output in the order defined by the `DimensionFormat`, not the order you call `Add`. The missing `Department` segment produces an empty placeholder between the delimiters.

## Handle Missing Segments

When a segment is not provided, the builder inserts an empty string in its position. This is required by F&O to maintain the structural integrity of the dimension string:

```csharp
// Only provide the last segment
string partial = builder
    .Initialize(format)
    .Add("CostCenter", "CC003")
    .Build();

Console.WriteLine(partial);
```

```text
--CC003
```

Both `BusinessUnit` and `Department` are empty, producing two leading delimiters.

## Provide All Segments

```csharp
string full = builder
    .Initialize(format)
    .Add("BusinessUnit", "BU01")
    .Add("Department", "DEPT10")
    .Add("CostCenter", "CC002")
    .Build();

Console.WriteLine(full);
```

```text
BU01-DEPT10-CC002
```

## Reuse the Builder with Clear

Call `Clear()` to reset the builder for constructing a second dimension string without creating a new instance:

```csharp
var builder = new FinancialDimensionBuilder();

// First dimension string
string first = builder
    .Initialize(format)
    .Add("BusinessUnit", "BU01")
    .Build();

// Reset and build another
builder.Clear();

string second = builder
    .Initialize(format)
    .Add("Department", "SALES")
    .Add("CostCenter", "CC005")
    .Build();

Console.WriteLine(first);
Console.WriteLine(second);
```

```text
BU01--
-SALES-CC005
```

Note that `Initialize` also calls `Clear` internally, so calling `Initialize` on an existing builder is equivalent to `Clear` followed by `Initialize`.

## Combine with a Real Query

In practice, fetch the `DimensionFormat` from F&O first, then feed it into the builder:

```csharp
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;

// 1. Query the dimension format from F&O (cached for 15 minutes)
var query = new GetDimensionOrdersQuery(
    "DefaultDimension",
    DimensionHierarchyType.DataEntityDefaultDimensionFormat);

Result<DimensionFormat> formatResult = await mediator.Send(query, cancellationToken);
// Result: Result<DimensionFormat> — cached for 15 minutes

// 2. Build the dimension string
if (formatResult.IsSuccess)
{
    string dimensions = new FinancialDimensionBuilder()
        .Initialize(formatResult.Value)
        .Add("BusinessUnit", "BU01")
        .Add("CostCenter", "CC002")
        .Build();

    Console.WriteLine(dimensions);
    // Output: BU01--CC002 (segment order from DimensionFormat, missing segments are empty)
}
```

## When Things Go Wrong

If you call `Build()` without calling `Initialize` first, the builder returns an empty string:

```csharp
var builder = new FinancialDimensionBuilder();
string result = builder.Add("BusinessUnit", "BU01").Build();

Console.WriteLine($"Result: '{result}'");
```

```text
Result: ''
```

If you pass `null` or whitespace as a segment name or value, the `Add` call is silently ignored:

```csharp
string result = builder
    .Initialize(format)
    .Add("BusinessUnit", "BU01")
    .Add("", "ignored")        // empty name — skipped
    .Add("CostCenter", "  ")   // whitespace value — skipped
    .Build();

Console.WriteLine(result);
```

```text
BU01--
```

## See Also

- [[Query-Dimension-Formats]] — fetch the `DimensionFormat` from F&O
- [[Create-a-Ledger-Journal]] — use dimension strings in journal line `AccountDisplayValue`
- [[Create-an-Entity]] — generic entity creation
