# Create Your First Entity

Define a C# class that maps to a D365 F&O data entity. This page assumes you have [[Install-the-Framework|installed the framework]].

## Define the Entity

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.Common.Annotations;

namespace MyProject.Domain.Entities;

[Table("LedgerJournalHeaders")]
public class LedgerJournalHeader : BaseEntity<string>
{
    [Key]
    [JsonPropertyName("dataAreaId")]
    public required string DataAreaId { get; set; }

    [Key]
    [JsonPropertyName("JournalBatchNumber")]
    [ODataField(IgnoreOnCreate = true)]
    public string? JournalBatchNumber { get; set; }

    [JsonPropertyName("JournalName")]
    public required string JournalName { get; set; }

    [JsonPropertyName("Description")]
    public required string Description { get; set; }

    public override object[] GetCompositeKey()
    {
        return [DataAreaId, JournalBatchNumber ?? "null"];
    }
}
```

## What Each Attribute Does

- **`[Table("LedgerJournalHeaders")]`** -- Maps the class to the OData entity set name. This is the URL segment used in requests (e.g. `/data/LedgerJournalHeaders`).
- **`[Key]`** -- Marks properties that form the composite primary key. D365 F&O entities typically have composite keys.
- **`[JsonPropertyName("...")]`** -- Maps the C# property to the OData field name in JSON payloads.
- **`[ODataField(IgnoreOnCreate = true)]`** -- Excludes the property from POST requests. Use this for server-generated fields like `JournalBatchNumber` (assigned by a number sequence in D365).
- **`[ODataField(IgnoreOnUpdate = true)]`** -- Excludes the property from PATCH requests. Use this for read-only fields that cannot change after creation.

## Base Class: `BaseEntity<TKey>`

All entities inherit from `BaseEntity<TKey>`, which implements `IEntity` and `IContext`:

```csharp
public abstract class BaseEntity<TKey> : IEntity, IContext
{
    public abstract object[] GetCompositeKey();
    public virtual IReadOnlyDictionary<string, object> GetLoggingContext();
}
```

- **`GetCompositeKey()`** -- Returns the key values as an array. The order must be consistent. This is used by queries like `GetByKeyQuery` and by the logging pipeline.
- **`GetLoggingContext()`** -- Returns all public properties as a dictionary for structured logging. You rarely need to override this.

## Entity with Read-Only Fields

For entities with server-calculated fields, use `[ODataField]` to control serialisation:

```csharp
[JsonPropertyName("JournalTotalDebit")]
[ODataField(IgnoreOnCreate = true, IgnoreOnUpdate = true)]
public decimal JournalTotalDebit { get; set; }

[JsonPropertyName("IsPosted")]
[ODataField(IgnoreOnCreate = true, IgnoreOnUpdate = true)]
public NoYes IsPosted { get; set; }
```

These fields are included when reading from D365 but excluded from create and update payloads.

## Common Mistakes

**Forgetting `[Table]`** -- Without the `[Table("EntitySetName")]` attribute, the OData client cannot determine which entity set to target. You will get a runtime error when sending commands.

**Inconsistent `GetCompositeKey()` order** -- The array order must match the order D365 F&O expects in the entity key. Swapping `DataAreaId` and `JournalBatchNumber` produces 404 responses.

## What Just Happened

- You created a C# class that maps to the `LedgerJournalHeaders` OData entity set in D365 F&O.
- The `[Key]` attributes define the composite primary key (`DataAreaId` + `JournalBatchNumber`).
- `[ODataField(IgnoreOnCreate = true)]` ensures the server-generated batch number is not sent on creation.
- `GetCompositeKey()` provides a consistent key representation for queries and logging.

## See Also

- [[Send-Your-First-Command]] — send the create command through MediatR
- [[Run-Your-First-Query]] — query the entity you just created
- [[Configure-the-OData-Connection]] — set up the OData connection first
