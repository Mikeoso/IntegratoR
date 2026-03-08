# IService\<TEntity\>

Generic service interface that abstracts CRUD and query operations for entity data access. Concrete implementations (e.g. `ODataService<TEntity>`) translate method calls into OData HTTP requests.

## Use the Interface

```csharp
public class JournalService
{
    private readonly IService<LedgerJournalHeader> _service;

    public JournalService(IService<LedgerJournalHeader> service) => _service = service;

    public async Task<Result<LedgerJournalHeader>> GetJournal(
        string batchNumber, string dataAreaId, CancellationToken ct)
    {
        return await _service.GetByKeyAsync([batchNumber, dataAreaId], ct)
            .ConfigureAwait(false);
    }
}
```

## Interface Definition

```csharp
public interface IService<TEntity> where TEntity : IEntity
```

| Type Parameter | Constraint | Description |
|----------------|------------|-------------|
| `TEntity` | `IEntity` | The entity type to operate on |

## Methods

### GetByKeyAsync

Retrieves a single entity by its composite key.

```csharp
Task<Result<TEntity>> GetByKeyAsync(
    object[] keyValues, CancellationToken cancellationToken = default)
```

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `keyValues` | `object[]` | Yes | -- | Composite key values matching the entity's `GetCompositeKey()` order |
| `cancellationToken` | `CancellationToken` | No | `default` | Cancellation token |

Translates to: `GET /data/EntitySet(Key1='val1',Key2='val2')`

### FindAsync

Retrieves entities matching a LINQ filter expression.

```csharp
Task<Result<IEnumerable<TEntity>>> FindAsync(
    Expression<Func<TEntity, bool>>? filter, CancellationToken cancellationToken = default)
```

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `filter` | `Expression<Func<TEntity, bool>>?` | No | -- | LINQ filter expression; `null` returns all entities |
| `cancellationToken` | `CancellationToken` | No | `default` | Cancellation token |

Translates to: `GET /data/EntitySet?$filter=...`

### AddAsync

Creates a new entity.

```csharp
Task<Result<TEntity>> AddAsync(
    TEntity entity, CancellationToken cancellationToken = default)
```

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `entity` | `TEntity` | Yes | -- | The entity to create |
| `cancellationToken` | `CancellationToken` | No | `default` | Cancellation token |

Translates to: `POST /data/EntitySet`

### UpdateAsync

Updates an existing entity.

```csharp
Task<Result<TEntity>> UpdateAsync(
    TEntity entity, CancellationToken cancellationToken = default)
```

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `entity` | `TEntity` | Yes | -- | The entity with updated values |
| `cancellationToken` | `CancellationToken` | No | `default` | Cancellation token |

Translates to: `PATCH /data/EntitySet(Key1='val1',Key2='val2')`

### DeleteAsync

Deletes an entity.

```csharp
Task<Result> DeleteAsync(
    TEntity entity, CancellationToken cancellationToken = default)
```

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `entity` | `TEntity` | Yes | -- | The entity to delete (composite key must be populated) |
| `cancellationToken` | `CancellationToken` | No | `default` | Cancellation token |

Translates to: `DELETE /data/EntitySet(Key1='val1',Key2='val2')`

## See Examples

### Direct usage without MediatR

```csharp
// Inject IService<LedgerJournalHeader> via DI
IService<LedgerJournalHeader> service = ...;

// Read
Result<LedgerJournalHeader> getResult = await service.GetByKeyAsync(
    ["USMF", "JBN-001"], cancellationToken);
// getResult.Value.JournalBatchNumber == "JBN-001"

// Find with filter
Result<IEnumerable<LedgerJournalHeader>> findResult = await service.FindAsync(
    h => h.DataAreaId == "USMF", cancellationToken);
// findResult.Value contains all USMF journals

// Find all (null filter)
Result<IEnumerable<LedgerJournalHeader>> allResult = await service.FindAsync(null, cancellationToken);
// allResult.Value contains all journal headers (use with caution on large datasets)

// Create
var newHeader = new LedgerJournalHeader
{
    JournalBatchNumber = "JBN-002",
    DataAreaId = "USMF",
    Description = "Vendor payments"
};
Result<LedgerJournalHeader> addResult = await service.AddAsync(newHeader, cancellationToken);
// addResult.Value contains the entity with server-generated fields

// Update
newHeader.Description = "Vendor payments - Q1";
Result<LedgerJournalHeader> updateResult = await service.UpdateAsync(newHeader, cancellationToken);
// updateResult.Value.Description == "Vendor payments - Q1"

// Delete
Result deleteResult = await service.DeleteAsync(newHeader, cancellationToken);
// deleteResult.IsSuccess == true
```

### Error handling

```csharp
Result<LedgerJournalHeader> result = await service.GetByKeyAsync(
    ["USMF", "NONEXISTENT"], cancellationToken);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    Console.WriteLine($"[{error?.Type}] {error?.Code}: {error?.Message}");
    // Output: [NotFound] OData.NotFound: Entity not found for key: NONEXISTENT
}
```

## Keep in Mind

- `IService<TEntity>` is the abstraction consumed by the generic CQRS handlers. Most application code should use MediatR commands/queries rather than calling `IService` directly.
- The concrete `ODataService<TEntity>` implementation is registered by the OData layer's DI extension methods.
- All methods propagate `CancellationToken` and return `Result` types -- they never throw for business-level failures.

## See Also

- [[API-Generic-Commands]] — commands that delegate to IService methods
- [[API-Generic-Queries]] — queries that delegate to IService methods
- [[API-BaseEntity]] — entity base class constrained by IService
- [[API-IntegrationError]] — error type returned by service operations
