# Define a Custom Entity

Create a new entity class that maps to a D365 F&O OData data entity. Entities inherit from `BaseEntity<TKey>`, use data annotations for key and table mapping, and implement `GetCompositeKey()`.

> **Prerequisites:** [[Install-the-Framework]]

## Create the Entity Class

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.Common.Annotations;

namespace MyProject.Domain.Entities;

[Table("CustomersV3")]
public class Customer : BaseEntity<string>
{
    [Key]
    [JsonPropertyName("dataAreaId")]
    public required string DataAreaId { get; set; }

    [Key]
    [JsonPropertyName("CustomerAccount")]
    [ODataField(IgnoreOnCreate = true)]
    public string? CustomerAccount { get; set; }

    [JsonPropertyName("CustomerGroupId")]
    public required string CustomerGroupId { get; set; }

    [JsonPropertyName("OrganizationName")]
    public required string OrganizationName { get; set; }

    [JsonPropertyName("SalesCurrencyCode")]
    public string? SalesCurrencyCode { get; set; }

    [JsonPropertyName("InvoiceAccount")]
    [ODataField(IgnoreOnUpdate = true)]
    public string? InvoiceAccount { get; set; }

    public override object[] GetCompositeKey()
    {
        return [DataAreaId, CustomerAccount ?? "null"];
    }
}
```

## Step-by-Step Attribute Guide

### 1. Set the OData Entity Set Name

```csharp
[Table("CustomersV3")]
public class Customer : BaseEntity<string>
```

The `[Table]` attribute specifies the OData entity set name. This must match the D365 F&O data entity's public collection name exactly (e.g. `CustomersV3`, `LedgerJournalHeaders`, `VendorsV2`).

### 2. Mark Key Properties

```csharp
[Key]
[JsonPropertyName("dataAreaId")]
public required string DataAreaId { get; set; }

[Key]
[JsonPropertyName("CustomerAccount")]
[ODataField(IgnoreOnCreate = true)]
public string? CustomerAccount { get; set; }
```

Every `[Key]` property becomes part of the OData URL key segment. D365 F&O entities almost always include `DataAreaId` as the first key. The order of `[Key]` properties must match the order expected by the OData endpoint.

### 3. Map JSON Property Names

```csharp
[JsonPropertyName("CustomerGroupId")]
public required string CustomerGroupId { get; set; }
```

`[JsonPropertyName]` maps the C# property to the exact JSON field name used by the D365 OData API. D365 field names are case-sensitive -- `dataAreaId` is lowercase, while most other fields use PascalCase.

### 4. Control Serialisation with ODataFieldAttribute

```csharp
[ODataField(IgnoreOnCreate = true)]
public string? CustomerAccount { get; set; }

[ODataField(IgnoreOnUpdate = true)]
public string? InvoiceAccount { get; set; }
```

| Attribute | When to Use |
|-----------|-------------|
| `IgnoreOnCreate = true` | Server-generated fields (number sequences, auto-assigned IDs) |
| `IgnoreOnUpdate = true` | Fields that cannot change after creation (immutable keys, system fields) |

### 5. Implement GetCompositeKey

```csharp
public override object[] GetCompositeKey()
{
    return [DataAreaId, CustomerAccount ?? "null"];
}
```

Returns the key values as an array in the same order as the `[Key]` attributes. This is used internally to construct OData URLs and for logging context.

## Use the Entity with Commands and Queries

Once defined, the entity works with all generic commands and queries:

```csharp
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using MediatR;

// Create
var customer = new Customer
{
    DataAreaId = "USMF",
    CustomerGroupId = "10",
    OrganizationName = "Contoso Ltd"
};

Result<Customer> createResult = await mediator.Send(
    new CreateCommand<Customer>(customer), cancellationToken);
// Result: Result<Customer> — Success with server-generated CustomerAccount populated

// Query by key
Result<Customer> getResult = await mediator.Send(
    new GetByKeyQuery<Customer>(new object[] { "USMF", "US-001" }),
    cancellationToken);
// Result: Result<Customer> — the matching customer or failure if not found

// Query by filter
Result<IEnumerable<Customer>> filterResult = await mediator.Send(
    new GetByFilterQuery<Customer>(c => c.DataAreaId == "USMF" && c.CustomerGroupId == "10"),
    cancellationToken);
// Result: Result<IEnumerable<Customer>> — matching customers or empty collection

// Update
customer.OrganizationName = "Contoso Ltd (Updated)";
Result<Customer> updateResult = await mediator.Send(
    new UpdateCommand<Customer>(customer), cancellationToken);
// Result: Result<Customer> — Success with updated entity

// Delete
Result<Customer> deleteResult = await mediator.Send(
    new DeleteCommand<Customer>(customer), cancellationToken);
// Result: Result<Customer> — Success if entity was deleted
```

## When Things Go Wrong

**Wrong [Table] name** -- if the entity set name does not match D365, you get a 404:

```
result.IsFailed  = true
result.GetError().Code     = "OData.Error"
result.GetError().Message  = "Resource not found for the segment 'CustomersV4'."
result.GetError().Type     = ErrorType.NotFound
```

**Missing [Key] attribute** -- if a key property is not marked with `[Key]`, the OData client cannot construct the correct URL for GET, PATCH, and DELETE operations.

**Wrong [JsonPropertyName]** -- if the JSON name does not match the D365 field name, the field will be silently ignored by D365 on create/update, or will always be null on read.

## See Also

- [[Create-an-Entity]] — use the custom entity in a create command
- [[Query-Entities-by-Key]] — query by the composite key you defined
- [[Handle-Errors-with-Result]] — handle errors when key mapping fails
- [[Batch-Multiple-Operations]] — use custom entities in batch operations
