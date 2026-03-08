# FOSettings

Configuration class for D365 Finance & Operations business-logic settings, primarily controlling financial dimension behaviour. Bound from the `"FOSettings"` section in `appsettings.json`.

## Configure the Settings

```json
{
  "FOSettings": {
    "DimensionFormatName": "MainAccount-BusinessUnit-CostCenter",
    "DimensionHierarchyType": "DataEntityLedgerDimensionFormat"
  }
}
```

```csharp
services.AddODataClientFOProxy(configuration);
// Settings are available via IOptions<FOSettings>
```

## Settings

| Setting | Type | Required | Default | Description |
|---------|------|----------|---------|-------------|
| `DimensionFormatName` | `string` | Yes | `""` | Name of the financial dimension format from D365 F&O setup |
| `DimensionHierarchyType` | `DimensionHierarchyType` | No | `AccountStructure` | Type of dimension hierarchy to use |

### DimensionFormatName

This corresponds to a setup record in D365 F&O found under **General ledger > Chart of accounts > Dimensions > Financial dimension formats**. The format defines which dimensions are included, their order, and the delimiter.

### DimensionHierarchyType Enum

Common values used in integrations:

| Value | Int | Description |
|-------|-----|-------------|
| `AccountStructure` | 0 | Primary chart of accounts structure |
| `DataEntityDefaultDimensionFormat` | 17 | Default dimension format (without main account) for data entities |
| `DataEntityLedgerDimensionFormat` | 18 | Ledger dimension format (main account + dimensions) for data entities |
| `DataEntityBudgetDimensionFormat` | 19 | Budget dimension format for data entities |
| `Focus` | 6 | Budgeting and planning structure |
| `Customer` | 7 | Dimensions linked to customer master records |
| `Vendor` | 8 | Dimensions linked to vendor master records |

## See Examples

### Typical Configuration

```json
{
  "FOSettings": {
    "DimensionFormatName": "MainAccount-BusinessUnit-Department-CostCenter",
    "DimensionHierarchyType": "DataEntityLedgerDimensionFormat"
  }
}
```

### Injecting in a Service

```csharp
public class DimensionService
{
    private readonly FOSettings _settings;

    public DimensionService(IOptions<FOSettings> options)
    {
        _settings = options.Value;
    }

    public string GetFormatName() => _settings.DimensionFormatName;
}
```

### Error Handling

Missing or incorrect `DimensionFormatName` does not cause startup errors. Failures occur at runtime when dimension operations reference the format:

```csharp
// If DimensionFormatName is empty or does not match D365 setup
// the query for dimension formats will return no results
// Result will contain IntegrationError with relevant failure details
```

## Configuration Section

```
appsettings.json
{
  "FOSettings": { ... }    <-- binds to FOSettings class
}
```

Registered via `AddODataClientFOProxy`:

```csharp
services.Configure<FOSettings>(configuration.GetSection("FOSettings"));
```

## See Also

- [[API-FinancialDimensionBuilder]] — uses dimension formats to build display values
- [[Query-Dimension-Formats]] — how to retrieve formats from D365 F&O
- [[API-ODataSettings]] — OData connection settings (separate from F&O business settings)
