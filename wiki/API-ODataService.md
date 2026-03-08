# ODataService\<TEntity\>

Generic service implementation for OData CRUD, query, and batch operations with automatic retry policies, error handling, and performance tracking.

## Use the Service

```csharp
// Inject via DI (registered automatically by AddODataClient)
public class MyFunction
{
    private readonly IODataService<LedgerJournalHeader> _service;
    private readonly IODataBatchService<LedgerJournalHeader> _batchService;

    public MyFunction(
        IODataService<LedgerJournalHeader> service,
        IODataBatchService<LedgerJournalHeader> batchService)
    {
        _service = service;
        _batchService = batchService;
    }
}
```

## Interfaces

`ODataService<TEntity>` implements three interfaces:

| Interface | Purpose |
|-----------|---------|
| `IService<TEntity>` | Base CRUD operations (add, get, find, update, delete) |
| `IODataService<TEntity>` | OData-specific queries (filter, expand, select, paging, count) |
| `IODataBatchService<TEntity>` | Atomic batch operations via OData `$batch` |

## Constructor

```csharp
public ODataService(
    IODataClientAdapter client,
    ILogger<ODataService<TEntity>> logger,
    AsyncRetryPolicy? retryPolicy = null)
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `client` | `IODataClientAdapter` | Yes | Adapter wrapping the OData client |
| `logger` | `ILogger<ODataService<TEntity>>` | Yes | Structured logger |
| `retryPolicy` | `AsyncRetryPolicy?` | No | Optional Polly retry policy for OData-level retries |

> **Note:** You do not construct `ODataService` directly. It is resolved via DI as `IService<T>`, `IODataService<T>`, or `IODataBatchService<T>`.

## IService\<TEntity\> Methods

### AddAsync

Creates a new entity via OData POST. Payload is built dynamically, respecting `ODataFieldAttribute` rules.

```csharp
Task<Result<TEntity>> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
```

```csharp
var header = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalName = "GenJrn",
    Description = "Monthly accruals"
};

Result<LedgerJournalHeader> result = await _service.AddAsync(header, cancellationToken);
// Result: Result<LedgerJournalHeader> — Success with server-generated fields populated

if (result.IsSuccess)
{
    // Server-generated JournalBatchNumber is populated
    string batchNumber = result.Value.JournalBatchNumber!;
}
```

### GetByKeyAsync

Retrieves a single entity by composite key via OData GET.

```csharp
Task<Result<TEntity>> GetByKeyAsync(object[] keyValues, CancellationToken cancellationToken = default)
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `keyValues` | `object[]` | Yes | Key values in the order of `[Key]` attributes on the entity |

```csharp
Result<LedgerJournalHeader> result = await _service.GetByKeyAsync(
    ["USMF", "JRN-000042"], cancellationToken);
// Result: Result<LedgerJournalHeader> — the matching entity or failure if not found

if (result.IsFailed)
{
    // result.Errors contains IntegrationError with ErrorType.NotFound
}
```

### FindAsync

Finds entities matching a filter expression via OData `$filter`.

```csharp
Task<Result<IEnumerable<TEntity>>> FindAsync(
    Expression<Func<TEntity, bool>>? filter,
    CancellationToken cancellationToken = default)
```

```csharp
Result<IEnumerable<LedgerJournalHeader>> result = await _service.FindAsync(
    h => h.DataAreaId == "USMF" && h.JournalName == "GenJrn", cancellationToken);
// Result: Result<IEnumerable<LedgerJournalHeader>> — matching entities or empty collection
```

### UpdateAsync

Updates an existing entity via OData PATCH.

```csharp
Task<Result<TEntity>> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
```

```csharp
header.Description = "Updated description";
Result<LedgerJournalHeader> result = await _service.UpdateAsync(header, cancellationToken);
// Result: Result<LedgerJournalHeader> — Success with updated entity
```

### DeleteAsync

Deletes an entity via OData DELETE. Treats `NotFound` as success (D365 idempotency).

```csharp
Task<Result> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
```

```csharp
Result result = await _service.DeleteAsync(header, cancellationToken);
// Result: Result — Success (even if entity was already deleted, D365 idempotency)
```

## IODataService\<TEntity\> Methods

### QueryAsync

Advanced query with full OData query options.

```csharp
Task<Result<IEnumerable<TEntity>>> QueryAsync(
    Expression<Func<TEntity, bool>>? filter = null,
    Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
    Expression<Func<TEntity, object>>? expand = null,
    Expression<Func<TEntity, object>>? select = null,
    int? skip = null,
    int? top = null,
    CancellationToken cancellationToken = default)
```

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `filter` | `Expression<Func<TEntity, bool>>?` | No | `null` | OData `$filter` expression |
| `orderBy` | `Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>?` | No | `null` | OData `$orderby` |
| `expand` | `Expression<Func<TEntity, object>>?` | No | `null` | OData `$expand` for navigation properties |
| `select` | `Expression<Func<TEntity, object>>?` | No | `null` | OData `$select` for field projection |
| `skip` | `int?` | No | `null` | OData `$skip` for paging |
| `top` | `int?` | No | `null` | OData `$top` for page size |
| `cancellationToken` | `CancellationToken` | No | `default` | Cancellation token |

```csharp
// Paged query with filter
Result<IEnumerable<LedgerJournalHeader>> result = await _service.QueryAsync(
    filter: h => h.DataAreaId == "USMF",
    top: 50,
    skip: 0,
    cancellationToken: cancellationToken);
// Result: Result<IEnumerable<LedgerJournalHeader>> — up to 50 entities matching filter

// Query with select projection
Result<IEnumerable<LedgerJournalHeader>> result = await _service.QueryAsync(
    select: h => new { h.JournalBatchNumber, h.Description },
    top: 100,
    cancellationToken: cancellationToken);
// Result: Result<IEnumerable<LedgerJournalHeader>> — up to 100 entities with selected fields only
```

### FindAll

Returns all entities. Use with caution on large datasets.

```csharp
Task<Result<IEnumerable<TEntity>>> FindAll(CancellationToken cancellationToken = default)
```

```csharp
// WARNING: May return thousands of records
Result<IEnumerable<LedgerJournalHeader>> result = await _service.FindAll(cancellationToken);
// Result: Result<IEnumerable<LedgerJournalHeader>> — all entities in the entity set
```

### CountAsync

Server-side count via OData `$count`. Returns only an integer, not the entities.

```csharp
Task<Result<int>> CountAsync(
    Expression<Func<TEntity, bool>>? filter = null,
    CancellationToken cancellationToken = default)
```

```csharp
Result<int> result = await _service.CountAsync(
    h => h.DataAreaId == "USMF" && h.IsPosted == NoYes.No, cancellationToken);

// Result: Result<int> — server-side count without transferring entities

if (result.IsSuccess)
{
    int unpostedCount = result.Value; // e.g. 42
}
```

## IODataBatchService\<TEntity\> Methods

All batch methods are atomic (all-or-nothing via OData `$batch`).

### AddBatchAsync

```csharp
Task<Result> AddBatchAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
```

```csharp
var lines = new List<LedgerJournalLine>
{
    new() { DataAreaId = "USMF", JournalBatchNumber = "JRN-001", DebitAmount = 1000m },
    new() { DataAreaId = "USMF", JournalBatchNumber = "JRN-001", CreditAmount = 1000m }
};

Result result = await _batchService.AddBatchAsync(lines, cancellationToken);
// Result: Result — Success or Failure (atomic, all-or-nothing)
```

### UpdateBatchAsync

```csharp
Task<Result> UpdateBatchAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
```

### DeleteBatchAsync

```csharp
Task<Result> DeleteBatchAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
```

## Handle Errors

All methods return `Result` or `Result<T>` from FluentResults. Errors are typed as `IntegrationError`:

```csharp
Result<LedgerJournalHeader> result = await _service.GetByKeyAsync(["USMF", "INVALID"], cancellationToken);

if (result.IsFailed)
{
    IntegrationError error = result.Errors.OfType<IntegrationError>().First();
    // error.Code    -> "LedgerJournalHeader.NotFound"
    // error.Message -> "Entity with the specified composite key was not found"
    // error.Type    -> ErrorType.NotFound
}
```

Validation errors are returned for null entities or empty keys without throwing exceptions:

```csharp
Result<LedgerJournalHeader> result = await _service.GetByKeyAsync([], cancellationToken);
// result.IsFailed == true
// ErrorType.Validation, "Key values cannot be null or empty"
```

## Understand the Internal Behaviour

### Entity Set Resolution

The entity set name is resolved from the `[Table]` attribute on the entity class. Falls back to the pluralised type name (appending "s") if the attribute is absent.

```csharp
[Table("LedgerJournalHeaders")]  // -> entity set name: "LedgerJournalHeaders"
public class LedgerJournalHeader : BaseEntity<string> { }
```

### Composite Key Building

Key values are mapped to `[Key]` properties on the entity. For single keys, the value is passed directly. For composite keys, a dictionary is built mapping JSON property names to values.

### Payload Creation

`CreatePayload` builds a dictionary from entity properties, excluding:
- Properties marked `[NotMapped]`
- Properties marked `[JsonIgnore]`
- Properties with `[ODataField(IgnoreOnCreate = true)]` on POST
- Properties with `[ODataField(IgnoreOnUpdate = true)]` on PATCH
- Properties with default values (null for reference types, default for value types)

### Caching

Property metadata, entity set names, and key properties are cached in static `ConcurrentDictionary` instances for performance.

## See Also

- [[API-ODataFieldAttribute]] — control property serialisation per operation
- [[API-ODataSettings]] — connection and resilience configuration
- [[API-AddODataClient]] — DI registration and Polly policies
- [[API-LedgerJournalHeader]] — example F&O entity
