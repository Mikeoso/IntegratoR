# FinancialDimensionBuilder

Builder class that constructs formatted financial dimension strings compatible with D365 F&O. Uses a fluent interface to assemble dimension values in the correct order as defined by the system's dimension format.

## Use the Builder

```csharp
using IntegratoR.OData.FO.Builders;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;

var format = new DimensionFormat
{
    Delimiter = "-",
    Segments = new List<string> { "MainAccount", "BusinessUnit", "Department", "CostCenter" }
};

var builder = new FinancialDimensionBuilder();
string displayValue = builder
    .Initialize(format)
    .Add("MainAccount", "110110")
    .Add("BusinessUnit", "001")
    .Add("CostCenter", "CC002")
    .Build();

// Output: "110110-001--CC002"
// Note: empty placeholder for missing "Department"
```

## Methods

### Initialize

Initialises the builder with a dimension format and resets any previous state.

```csharp
public FinancialDimensionBuilder Initialize(DimensionFormat format)
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `format` | `DimensionFormat` | Yes | Defines segment order and delimiter |

**Returns:** The builder instance for fluent chaining.

### Add

Adds or updates a dimension segment value. Order of calls does not matter -- the output order is determined by the format.

```csharp
public FinancialDimensionBuilder Add(string name, string value)
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | `string` | Yes | Dimension segment name (e.g. "BusinessUnit") |
| `value` | `string` | Yes | Dimension value (e.g. "001") |

**Returns:** The builder instance for fluent chaining.

> Null, empty, or whitespace-only names and values are silently ignored.

### Build

Constructs the final delimited string using only the dimension values in segment order.

```csharp
public string Build()
```

**Returns:** A formatted dimension string (e.g. "110110-001--CC002"), or an empty string if the builder was not initialised.

### Clear

Resets the builder by clearing all dimensions and the format. Called automatically by `Initialize`.

```csharp
public void Clear()
```

## DimensionFormat

```csharp
public class DimensionFormat
{
    public required string Delimiter { get; set; }
    public List<string> Segments { get; set; } = new();
}
```

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Delimiter` | `string` | Yes | Character(s) separating segments (typically "-") |
| `Segments` | `List<string>` | No | Ordered list of dimension segment names |

## See Examples

### Basic Dimension String

```csharp
var format = new DimensionFormat
{
    Delimiter = "-",
    Segments = new List<string> { "BusinessUnit", "Department", "CostCenter" }
};

var builder = new FinancialDimensionBuilder();
string result = builder
    .Initialize(format)
    .Add("BusinessUnit", "BU01")
    .Add("Department", "SALES")
    .Add("CostCenter", "CC002")
    .Build();

// Output: "BU01-SALES-CC002"
```

### Omitted Segments

Missing segments produce empty placeholders to maintain positional integrity:

```csharp
string result = builder
    .Initialize(format)
    .Add("BusinessUnit", "BU01")
    .Add("CostCenter", "CC002")
    .Build();

// Output: "BU01--CC002"
// Department is empty but the delimiter is preserved
```

### Reusing the Builder

```csharp
var builder = new FinancialDimensionBuilder();

// First dimension string
string dims1 = builder
    .Initialize(format)
    .Add("BusinessUnit", "BU01")
    .Build();
// "BU01--"

// Second dimension string (Initialize clears previous state)
string dims2 = builder
    .Initialize(format)
    .Add("Department", "HR")
    .Add("CostCenter", "CC003")
    .Build();
// "-HR-CC003"
```

### With Journal Line AccountDisplayValue

```csharp
var accountFormat = new DimensionFormat
{
    Delimiter = "-",
    Segments = new List<string> { "MainAccount", "BusinessUnit", "CostCenter" }
};

var builder = new FinancialDimensionBuilder();
string accountDisplayValue = builder
    .Initialize(accountFormat)
    .Add("MainAccount", "110110")
    .Add("BusinessUnit", "001")
    .Add("CostCenter", "025")
    .Build();
// "110110-001-025"

var line = new LedgerJournalLine
{
    DataAreaId = "USMF",
    JournalBatchNumber = "JRN-000042",
    AccountDisplayValue = accountDisplayValue,
    AccountType = LedgerJournalACType.Ledger,
    CurrencyCode = "USD",
    DebitAmount = 5000.00m,
    TransDate = DateTimeOffset.Now
};
```

### Error Handling

The builder does not throw exceptions. Edge cases return empty strings:

```csharp
// Not initialised
var builder = new FinancialDimensionBuilder();
string result = builder.Build();
// Output: ""

// Initialised with empty segments
builder.Initialize(new DimensionFormat { Delimiter = "-", Segments = new() });
result = builder.Build();
// Output: ""

// Null/empty values are silently ignored
builder.Initialize(format);
builder.Add("BusinessUnit", "");  // ignored
builder.Add("", "value");         // ignored
builder.Add(null!, null!);        // ignored
result = builder.Build();
// Output: "--" (all segments empty)
```

## See Also

- [[API-LedgerJournalLine]] — uses dimension strings for account display values
- [[Build-Financial-Dimension-Strings]] — step-by-step how-to guide
- [[API-FOSettings]] — dimension format configuration
