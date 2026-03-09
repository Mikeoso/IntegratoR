# Entities

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.Common.Annotations;

namespace MyProject.Domain.Entities;

[Table("CustomersV3")]                              // OData entity set name -> /data/CustomersV3
public class Customer : BaseEntity<string>          // BaseEntity<TKey> where TKey is the key type
{
    [Key]
    [JsonPropertyName("dataAreaId")]
    public required string DataAreaId { get; set; }

    [Key]
    [JsonPropertyName("CustomerAccount")]
    [ODataField(IgnoreOnCreate = true)]             // server-generated via number sequence
    public string? CustomerAccount { get; set; }

    [JsonPropertyName("CustomerGroupId")]
    public required string CustomerGroupId { get; set; }

    [JsonPropertyName("OrganizationName")]
    public required string OrganizationName { get; set; }

    [JsonPropertyName("SalesCurrencyCode")]
    public string? SalesCurrencyCode { get; set; }

    [JsonPropertyName("InvoiceAccount")]
    [ODataField(IgnoreOnUpdate = true)]             // immutable after creation
    public string? InvoiceAccount { get; set; }

    public override object[] GetCompositeKey()      // key order must match D365 expectation
    {
        return [DataAreaId, CustomerAccount ?? "null"];
    }
}
```

## BaseEntity\<TKey\>

All entities inherit from `BaseEntity<TKey>` (`abstract class BaseEntity<TKey> : IEntity, IContext`). The `TKey` type parameter is a semantic marker for the key type (e.g. `string`, `long`, `Guid`).

**`GetCompositeKey()`** (abstract) -- returns the values that uniquely identify the entity. The array order must match the key order the OData endpoint expects. Used internally for URL construction and by the [[Queries|query handlers]].

```csharp
public override object[] GetCompositeKey() => [DataAreaId, SalesOrderNumber, LineNumber];
```

**`GetLoggingContext()`** (virtual) -- returns all public properties as a dictionary via reflection. The [[Extending-the-Pipeline|LoggingBehaviour]] uses this for structured log entries. Override it to exclude sensitive fields:

```csharp
public override IReadOnlyDictionary<string, object> GetLoggingContext()
    => new Dictionary<string, object> { { "Id", Id } }; // exclude Secret
```

## ODataFieldAttribute

`[ODataField]` (`[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]`) controls whether a property is serialised into POST or PATCH payloads.

| Property | Default | Effect |
|----------|---------|--------|
| `IgnoreOnCreate` | `false` | `true` excludes from POST -- use for server-generated fields (number sequences, auto-assigned IDs) |
| `IgnoreOnUpdate` | `false` | `true` excludes from PATCH -- use for immutable fields that cannot change after creation |

```csharp
// Server-generated key -- excluded from create, included in update
[ODataField(IgnoreOnCreate = true)]
public string? JournalBatchNumber { get; set; }

// Server-generated line number -- excluded from create (LedgerJournalLine)
[ODataField(IgnoreOnCreate = true)]
public decimal LineNumber { get; set; }               // assigned by D365 number sequence

// Immutable after creation -- included in create, excluded from update
[ODataField(IgnoreOnUpdate = true)]
public required string ReadOnlyField { get; set; }    // e.g. a field that cannot change after creation
```

The resulting payloads sent by [[Commands|ODataService\<TEntity\>]]:

```csharp
// POST /data/LedgerJournalHeaders
// { "dataAreaId": "USMF", "JournalName": "GenJrn", "Description": "Accruals" }
// JournalBatchNumber excluded (IgnoreOnCreate)

// PATCH /data/LedgerJournalHeaders(dataAreaId='USMF',JournalBatchNumber='JRN-001')
// { "Description": "Updated" }
// ReadOnlyField excluded (IgnoreOnUpdate)
```

## Attribute Reference

- **`[Table("EntitySetName")]`** -- maps the class to the OData entity set URL segment. Must match the D365 F&O data entity's public collection name exactly.
- **`[Key]`** -- marks properties forming the composite primary key. D365 entities almost always include `DataAreaId` as the first key.
- **`[JsonPropertyName("...")]`** -- maps C# properties to OData JSON field names. D365 field names are case-sensitive (`dataAreaId` is lowercase, most others are PascalCase).

## Using Entities with Commands and Queries

Once defined, an entity works with all [[Commands|generic commands]] and [[Queries|generic queries]]:

```csharp
var customer = new Customer
{
    DataAreaId = "USMF",
    CustomerGroupId = "10",
    OrganizationName = "Contoso Ltd"
};

Result<Customer> created = await mediator.Send(
    new CreateCommand<Customer>(customer), ct);       // CustomerAccount populated by server

Result<Customer> fetched = await mediator.Send(
    new GetByKeyQuery<Customer>(["USMF", "US-001"]), ct);

Result<IEnumerable<Customer>> filtered = await mediator.Send(
    new GetByFilterQuery<Customer>(c => c.DataAreaId == "USMF"), ct);

customer.OrganizationName = "Contoso Ltd (Updated)";
Result<Customer> updated = await mediator.Send(
    new UpdateCommand<Customer>(customer), ct);       // InvoiceAccount excluded (IgnoreOnUpdate)

Result<Customer> deleted = await mediator.Send(
    new DeleteCommand<Customer>(customer), ct);
```

## See Also

- [[Commands]] — generic CRUD commands and `IService<T>`
- [[Queries]] — query by key or filter expression
- [[D365-FO-Journals]] — pre-built D365 F&O journal entities
