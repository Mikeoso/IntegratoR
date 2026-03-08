# BaseEntity\<TKey\>

Abstract base class for domain entities. Provides composite key support and reflection-based logging context, both essential for D365 F&O integrations where entities use multi-field primary keys.

## Use the Base Class

```csharp
public class LedgerJournalHeader : BaseEntity<string>
{
    public required string JournalBatchNumber { get; set; }
    public required string DataAreaId { get; set; }
    public string? Description { get; set; }

    public override object[] GetCompositeKey() => [DataAreaId, JournalBatchNumber];
}
```

## Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TKey` | The data type of the entity's primary key (e.g. `string`, `long`, `Guid`) |

## Class Definition

```csharp
public abstract class BaseEntity<TKey> : IEntity, IContext
```

### Members

| Member | Type | Description |
|--------|------|-------------|
| `GetCompositeKey()` | `abstract object[]` | Returns the values that uniquely identify this entity. Must be overridden. |
| `GetLoggingContext()` | `virtual IReadOnlyDictionary<string, object>` | Uses reflection to capture all public properties as key-value pairs for structured logging. |

## IEntity Interface

```csharp
public interface IEntity
{
    object[] GetCompositeKey();
    IReadOnlyDictionary<string, object> GetLoggingContext();
}
```

All generic commands and queries constrain their type parameter to `IEntity`, so every entity used with the CQRS pipeline must implement this interface (typically via `BaseEntity<TKey>`).

## See Examples

### D365 F&O entity with composite key

```csharp
public class SalesOrderLine : BaseEntity<long>
{
    public required string SalesOrderNumber { get; set; }
    public required decimal LineNumber { get; set; }
    public required string DataAreaId { get; set; }
    public string? ItemId { get; set; }
    public decimal Quantity { get; set; }

    public override object[] GetCompositeKey()
        => [DataAreaId, SalesOrderNumber, LineNumber];
}

var line = new SalesOrderLine
{
    SalesOrderNumber = "SO-001",
    LineNumber = 1.0m,
    DataAreaId = "USMF",
    ItemId = "D0001",
    Quantity = 10
};

line.GetCompositeKey();
// Output: ["USMF", "SO-001", 1.0]
```

### Logging context output

```csharp
IReadOnlyDictionary<string, object> context = line.GetLoggingContext();
// Output:
// {
//   "SalesOrderNumber": "SO-001",
//   "LineNumber": 1.0,
//   "DataAreaId": "USMF",
//   "ItemId": "D0001",
//   "Quantity": 10
// }
```

The `LoggingBehaviour` uses this dictionary to enrich structured log entries with the entity's state.

### Overriding GetLoggingContext

```csharp
public class SensitiveEntity : BaseEntity<string>
{
    public required string Id { get; set; }
    public required string Secret { get; set; }

    public override object[] GetCompositeKey() => [Id];

    public override IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object> { { "Id", Id } }; // Exclude Secret
}
```

### Error: forgetting GetCompositeKey

```csharp
// This will not compile -- GetCompositeKey() is abstract
public class BadEntity : BaseEntity<string>
{
    public required string Id { get; set; }
    // ERROR CS0534: 'BadEntity' does not implement inherited abstract member
    // 'BaseEntity<string>.GetCompositeKey()'
}
```

## Keep in Mind

- The order of values returned by `GetCompositeKey()` is significant. It must match the key order expected by the OData endpoint.
- `GetLoggingContext()` uses reflection, which is acceptable for logging but should not be used in hot paths. The default implementation excludes indexed properties and replaces null values with `new object()`.
- Entities do not need to use `TKey` directly in their properties -- it serves as a semantic marker for the key type.

## See Also

- [[API-Generic-Commands]] — commands that operate on BaseEntity types
- [[API-Generic-Queries]] — queries that return BaseEntity types
- [[API-IService]] — service interface constrained to IEntity
