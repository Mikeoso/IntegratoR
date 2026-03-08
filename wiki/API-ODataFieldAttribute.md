# ODataFieldAttribute

Property-level attribute that controls whether a property is included in OData POST (create) or PATCH (update) payloads.

## Use the Attribute

```csharp
using IntegratoR.OData.Common.Annotations;

[Table("SalesOrders")]
public class SalesOrder : BaseEntity<string>
{
    [Key]
    [JsonPropertyName("dataAreaId")]
    public required string DataAreaId { get; set; }

    // Server-generated on create, read-only after
    [Key]
    [JsonPropertyName("SalesOrderNumber")]
    [ODataField(IgnoreOnCreate = true, IgnoreOnUpdate = true)]
    public string? SalesOrderNumber { get; set; }

    // Editable on create and update
    [JsonPropertyName("CustomerAccount")]
    public required string CustomerAccount { get; set; }

    // Set by server on create, not changeable after
    [JsonPropertyName("OrderStatus")]
    [ODataField(IgnoreOnCreate = true, IgnoreOnUpdate = true)]
    public string? OrderStatus { get; set; }
}
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IgnoreOnCreate` | `bool` | `false` | When `true`, excludes this property from the POST payload |
| `IgnoreOnUpdate` | `bool` | `false` | When `true`, excludes this property from the PATCH payload |

## See Examples

### Server-Generated Fields

Fields like batch numbers or line numbers that D365 F&O assigns automatically:

```csharp
[Key]
[JsonPropertyName("JournalBatchNumber")]
[ODataField(IgnoreOnCreate = true)]
public string? JournalBatchNumber { get; set; }
```

The property is excluded from the POST payload but included in PATCH, allowing updates to reference the key.

### Read-Only Fields

Fields that cannot be changed after creation:

```csharp
[JsonPropertyName("IsPosted")]
[ODataField(IgnoreOnUpdate = true)]
public virtual NoYes IsPosted { get; set; }
```

### Fields Ignored on Both Operations

System-calculated or purely read-only fields:

```csharp
[JsonPropertyName("JournalTotalDebit")]
[ODataField(IgnoreOnCreate = true, IgnoreOnUpdate = true)]
public virtual decimal JournalTotalDebit { get; set; }
```

### How It Works with CreatePayload

`ODataService<TEntity>` inspects `ODataFieldAttribute` at runtime when building request payloads:

```
POST /data/LedgerJournalHeaders
{
    "dataAreaId": "USMF",
    "JournalName": "GenJrn",        // No attribute -> included
    "Description": "Accruals"       // No attribute -> included
    // JournalBatchNumber excluded  -> IgnoreOnCreate = true
}
```

```
PATCH /data/LedgerJournalHeaders(dataAreaId='USMF',JournalBatchNumber='JRN-001')
{
    "Description": "Updated"        // No attribute -> included
    // IsPosted excluded            -> IgnoreOnUpdate = true
}
```

### Error Handling

The attribute itself does not produce errors. If a required field is excluded and the server rejects the request, the error surfaces through the `Result` return value:

```csharp
var result = await service.AddAsync(entity, cancellationToken);

if (result.IsFailed)
{
    // D365 may return 400 if a required field was excluded
    // Check ODataFieldAttribute configuration on the entity
}
```

## Attribute Metadata

```csharp
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public class ODataFieldAttribute : Attribute
```

- Targets properties only
- Inherited by derived entity classes
- One attribute per property

## See Also

- [[API-ODataService]] — uses this attribute to build payloads
- [[API-LedgerJournalHeader]] — real-world entity using this attribute
- [[API-LedgerJournalLine]] — entity with extensive IgnoreOnCreate usage
