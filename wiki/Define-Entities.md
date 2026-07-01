# Define Entities

> Last verified against v2.0.1

Every D365 F&O data entity you read or write needs a C# class. Inherit the non-generic `BaseEntity`, map the class to its OData entity set with `[Table]`, declare the composite key with `[Key]` plus `GetCompositeKey()`, and control per-property serialisation with `[ODataField]` and `[JsonPropertyName]`.

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.Common.Annotations;

namespace MyProject.Domain.Entities;

[Table("LedgerJournalHeaders")]
public class LedgerJournalHeader : BaseEntity
{
    [Key]
    [JsonPropertyName("dataAreaId")]
    [ODataField(IsRequired = true)]
    public required string DataAreaId { get; set; }

    [Key]
    [JsonPropertyName("JournalBatchNumber")]
    [ODataField(IgnoreOnCreate = true)]   // D365 number sequence assigns it on create
    public string? JournalBatchNumber { get; set; }

    [JsonPropertyName("JournalName")]
    [ODataField(IgnoreOnUpdate = true)]   // read-only after create
    public required string JournalName { get; set; }

    [JsonPropertyName("Description")]
    public required string Description { get; set; }

    public override object[] GetCompositeKey() => [DataAreaId, JournalBatchNumber!];
}
```

Send it as a command over `IMediator` and you get a `Result<LedgerJournalHeader>` back — see [Send Commands](Send-Commands).

Three attributes make this entity work end to end:

- `[Table("LedgerJournalHeaders")]` maps the class to the D365 OData entity set. The name is case-sensitive and almost always plural — `LedgerJournalHeader` is the entity, `LedgerJournalHeaders` is the entity set.
- `[Key]` marks each composite-key property; `GetCompositeKey()` returns those same fields in the same order.
- `[ODataField]` and `[JsonPropertyName]` control what is sent on the wire and under which name.

## Inherit BaseEntity

`BaseEntity` is the non-generic `abstract` class in `IntegratoR.Abstractions.Domain.Entities`. It implements `IEntity` and `IContext`, so a custom entity rarely needs either interface directly. Inherit it and override the one abstract member, `object[] GetCompositeKey()`. Because the key is an `object[]`, one base class serves every key shape:

```csharp
public class DimensionParameters : BaseEntity
{
    [Key]
    [JsonPropertyName("Key")]
    public required int Key { get; set; }

    public override object[] GetCompositeKey() => [Key];
}
```

> [!WARNING]
> Never inherit `BaseEntity<TKey>`. Its `TKey` parameter was never used, it is `[Obsolete]` since v1.4.0, and it is removed in the next MAJOR. Derive every new entity from the non-generic `BaseEntity`.

## Declare the composite key

D365 F&O entities are almost always identified by a composite key: a header keys on `DataAreaId` + `JournalBatchNumber`, a line adds `LineNumber` as a third part. Mark every key property with `[Key]` and return the same fields from `GetCompositeKey()` in the same order:

```csharp
public override object[] GetCompositeKey() => [DataAreaId, JournalBatchNumber!, LineNumber];
```

The order is load-bearing. The OData URL the framework builds for `GetByKeyQuery<T>`, `UpdateCommand<T>`, and `DeleteCommand<T>` zips each value to its `[Key]` property by declaration order. A mismatch between `[Key]` order and `GetCompositeKey()` order produces the wrong lookup URL.

Use the null-forgiving `!` on a server-generated key part such as `JournalBatchNumber` — never `?? "null"`. A real `null` element flows through to a `Validation` failure rather than silently searching for the literal string `"null"`. Populate every key component before you send an `UpdateCommand<T>`, `DeleteCommand<T>`, or `GetByKeyQuery<T>`; a null part fails fast, before any HTTP call:

```csharp
// JournalBatchNumber left null on an Update
Result<LedgerJournalHeader> result = await mediator.Send(new UpdateCommand<LedgerJournalHeader>(header));

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    // Code: "LedgerJournalHeader.InvalidKey"
    // Message: "Composite key element at index 1 for field 'JournalBatchNumber' was null; a null key cannot identify an entity."
    // Type: ErrorType.Validation
}
```

## Control serialisation with `[ODataField]`

`[ODataField]` decides which properties appear in the create (POST) and update (PATCH) payloads. The framework reflects on the attribute inside `ODataService<T>` and strips matching properties from the JSON body — you populate the full entity, the framework filters at serialisation time.

| Property | Default | Effect |
|---|---|---|
| `IgnoreOnCreate` | `false` | Exclude from the POST payload — number-sequence fields, server defaults |
| `IgnoreOnUpdate` | `false` | Exclude from the PATCH payload — key parts, immutable business keys, server-computed / status fields |
| `AllowEdit` | `true` | CSDL-derived; `false` excludes from update, same as `IgnoreOnUpdate` |
| `AllowEditOnCreate` | `true` | CSDL-derived; `false` excludes from create, same as `IgnoreOnCreate` |
| `IsRequired` | `false` | Marks a non-nullable D365 field; a null value on create fails with a `Validation` error |
| `EdmType`, `Label` | `null` | Informational CSDL metadata; not used at serialisation time |

The effective exclusion combines the hand-written flag with the CSDL-derived flag:

```
Excluded from create = IgnoreOnCreate    OR AllowEditOnCreate == false
Excluded from update = IgnoreOnUpdate    OR AllowEdit         == false
```

Hand-written entities use the plain `IgnoreOn*` flags; generated entities carry the CSDL `AllowEdit*` flags as the source of truth. The framework honours either.

> [!WARNING]
> If a PATCH body contains any field D365 treats as read-only on update, D365 rejects the whole PATCH with an `ODataSecurityException` — HTTP 403, `"update not allowed for field 'X'"` — not only that field. On `LedgerJournalHeader` this covers `JournalName`, `AccountingCurrency`, `IsPosted`, `JournalTotalDebit`, and `JournalTotalCredit`. Mark every such field `[ODataField(IgnoreOnUpdate = true)]`. Verified against live D365 (JFI) on 2026-07-01.

Setting a value on a field marked `IgnoreOnCreate = true` and calling `CreateCommand<T>` drops that value: the record is created with D365's server default instead. Read the entity source for its `[ODataField]` matrix before you populate it.

## Map property names with `[JsonPropertyName]`

D365 F&O exposes roughly 19,600 PascalCase fields and 479 camelCase legacy X++ system fields (`dataAreaId`, `recId`, `validFrom`, `validTo`, `itemId`, `custAccount`, `transDate`, …). Declare the CLR property in PascalCase by C# convention and pin the wire name with `[JsonPropertyName]`:

```csharp
[JsonPropertyName("dataAreaId")]   // camelCase wire name, PascalCase CLR property
public required string DataAreaId { get; set; }

[JsonPropertyName("JournalName")]  // PascalCase both sides — the common case
public required string JournalName { get; set; }
```

The IntegratoR LINQ-to-OData translator honours `[JsonPropertyName]` in filter, select, expand, and `$orderby` expressions. A predicate `h => h.DataAreaId == "USMF"` emits `$filter=dataAreaId eq 'USMF'`. Without the attribute the translator would emit `DataAreaId eq 'USMF'` and D365 would answer *"Could not find a property named 'DataAreaId'"*. Never write raw OData filter strings — use typed LINQ throughout (see [Run Queries](Run-Queries)).

## Declare enum properties

D365 enums arrive as string-valued members over OData (`"PostingLayer": "Current"`). Declare a CLR enum property and let the global `JsonStringEnumConverter`, registered by `AddIntegratoR`, round-trip it:

```csharp
[JsonPropertyName("PostingLayer")]
public virtual CurrentOperationsTax PostingLayer { get; set; }

[JsonPropertyName("IsPosted")]
[ODataField(IgnoreOnUpdate = true)]
public virtual NoYes IsPosted { get; set; }
```

The filter translator emits the qualified-type form D365 requires for enum comparisons, in both top-level predicates and `Any`/`All` lambda bodies:

```csharp
h => h.IsPosted == NoYes.Yes
// emits: $filter=IsPosted eq Microsoft.Dynamics.DataEntities.NoYes'Yes'
```

## Shipped entities

`IntegratoR.OData.FO` bundles two ready-made entities for the general-ledger journal flow. Read the source under `IntegratoR.OData.FO/Domain/Entities/LedgerJournal/` for the full attribute matrix.

| Entity | Table | Composite key |
|---|---|---|
| `LedgerJournalHeader` | `LedgerJournalHeaders` | `(DataAreaId, JournalBatchNumber)` |
| `LedgerJournalLine` | `LedgerJournalLines` | `(DataAreaId, JournalBatchNumber, LineNumber)` |

`LedgerJournalHeader` write flags:

| Field | Wire name | Flags |
|---|---|---|
| `DataAreaId` | `dataAreaId` | `IsRequired` |
| `JournalBatchNumber` | `JournalBatchNumber` | `IgnoreOnCreate` |
| `JournalName` | `JournalName` | required, `IgnoreOnUpdate` |
| `Description` | `Description` | required |
| `IsPosted` | `IsPosted` | `IgnoreOnUpdate` |
| `JournalTotalDebit` / `JournalTotalCredit` | same | `IgnoreOnUpdate` |
| `AccountingCurrency` | `AccountingCurrency` | `IgnoreOnUpdate` |

`LedgerJournalLine` marks its `LineNumber` key `IgnoreOnCreate` (server-assigned) and around two dozen further fields `IgnoreOnCreate`. Note `TransactionText` maps to the wire name `Text`. A minimal create needs `DataAreaId`, `JournalBatchNumber`, `AccountDisplayValue`, `AccountType`, `DebitAmount`, `CreditAmount`, `CurrencyCode`, and `TransDate`; the required amount and currency fields carry `IsRequired`.

## Extend a shipped entity

Subclass `LedgerJournalHeader` or `LedgerJournalLine` to add a field D365 exposes but the built-in class omits, or to override an `[ODataField]` flag that is wrong for your integration:

```csharp
using System.Text.Json.Serialization;
using IntegratoR.OData.Common.Annotations;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.FO.Domain.Enums.LedgerJournals;

namespace MyProject.Domain.Entities;

// No [Table] needed — inherited from LedgerJournalLine.
public class MyLedgerJournalLine : LedgerJournalLine
{
    // Add a field the built-in entity does not declare.
    [JsonPropertyName("CustomReference")]
    public string? CustomReference { get; set; }

    // Override a flag by re-declaring the attribute on the overriding property.
    [ODataField(IgnoreOnCreate = false)]
    [JsonPropertyName("AccountType")]
    public override LedgerJournalACType AccountType { get; set; }
}
```

Three rules make this work:

1. `[Table]`, `[Key]`, and `GetCompositeKey()` are inherited. A subclass targeting the same entity set needs none of them. Re-declare `[Table("…")]` only to point the subclass at a different set, and override `GetCompositeKey()` only if the key shape changes.
2. Re-declare the attribute, not only the property. `ODataFieldAttribute` is `Inherited = true` and `AllowMultiple = false`, so overriding a property without re-applying `[ODataField]` keeps the base flag; re-declaring it with the corrected value wins because the payload builder reflects on the instance's runtime type.
3. The overridden property must be `virtual`. Every shipped field is `virtual` except the server-assigned `LineNumber` / `JournalBatchNumber` keys, which you never override. `new`-shadowing a non-virtual property makes reflection see both properties and emit a duplicate wire field.

### Register the extended entity

Subclassing alone does not make `mediator.Send(new CreateCommand<MyLedgerJournalLine>(...))` work — the generic handlers, validators, and service must close over your type. Hand the assembly holding your entities to `AddConsumerHandlers`; `AddIntegratoR` folds it into the same scan it runs for the framework's own entities:

```csharp
services.AddIntegratoR(configuration, integrator =>
    integrator.AddConsumerHandlers(typeof(MyLedgerJournalLine).Assembly));
```

That one call closes the full generic surface — `CreateCommand<T>`, `UpdateCommand<T>`, `DeleteCommand<T>`, `GetByKeyQuery<T>`, `GetByFilterQuery<T>` — over `MyLedgerJournalLine` and registers its FluentValidation validators.

## See Also

- [Send Commands](Send-Commands) — create, update, and delete the entity via `IMediator`
- [Run Queries](Run-Queries) — read by composite key or by a typed LINQ filter
- [Work with Dimensions](Work-with-Dimensions) — the dimension entities and their format
- [Troubleshoot Common Issues](Troubleshoot-Common-Issues) — errors when extending or registering custom entities
