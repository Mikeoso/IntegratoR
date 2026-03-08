# Generic Commands

Six pre-built command records for standard CRUD operations. Single-entity commands return `Result<TEntity>`, batch commands return `Result` (non-generic). All are generic over any `IEntity` type.

## Use the Generic Commands

```csharp
var header = new LedgerJournalHeader
{
    JournalBatchNumber = "JBN-001",
    DataAreaId = "USMF",
    Description = "Monthly accruals"
};

Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(header),
    cancellationToken);
// result.Value contains the created entity with server-generated fields populated
```

## Single-Entity Commands

### CreateCommand\<TEntity\>

```csharp
public record CreateCommand<TEntity>(TEntity Entity) : ICommand<Result<TEntity>>
    where TEntity : IEntity
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Entity` | `TEntity` | Yes | The entity to create |

### UpdateCommand\<TEntity\>

```csharp
public record UpdateCommand<TEntity>(TEntity Entity) : ICommand<Result<TEntity>>
    where TEntity : IEntity
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Entity` | `TEntity` | Yes | The entity with updated values |

### DeleteCommand\<TEntity\>

```csharp
public record DeleteCommand<TEntity>(TEntity Entity) : ICommand<Result<TEntity>>
    where TEntity : IEntity
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Entity` | `TEntity` | Yes | The entity to delete (must have composite key populated) |

## Batch Commands

Batch commands operate on collections and return non-generic `Result`.

### CreateBatchCommand\<TEntity\>

```csharp
public record CreateBatchCommand<TEntity>(IEnumerable<TEntity> Entities) : ICommand<Result>
    where TEntity : IEntity
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Entities` | `IEnumerable<TEntity>` | Yes | The entities to create |

### UpdateBatchCommand\<TEntity\>

```csharp
public record UpdateBatchCommand<TEntity>(IEnumerable<TEntity> Entities) : ICommand<Result>
    where TEntity : IEntity
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Entities` | `IEnumerable<TEntity>` | Yes | The entities to update |

### DeleteBatchCommand\<TEntity\>

```csharp
public record DeleteBatchCommand<TEntity>(IEnumerable<TEntity> Entities) : ICommand<Result>
    where TEntity : IEntity
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Entities` | `IEnumerable<TEntity>` | Yes | The entities to delete |

## See Examples

### Create

```csharp
var entity = new LedgerJournalHeader
{
    JournalBatchNumber = "JBN-001",
    DataAreaId = "USMF",
    Description = "Vendor payments"
};

Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(entity),
    cancellationToken);

if (result.IsSuccess)
    Console.WriteLine($"Created: {result.Value.JournalBatchNumber}");
// Output: Created: JBN-001
```

### Update

```csharp
entity.Description = "Vendor payments - updated";

Result<LedgerJournalHeader> result = await mediator.Send(
    new UpdateCommand<LedgerJournalHeader>(entity),
    cancellationToken);
// result.Value.Description == "Vendor payments - updated"
```

### Delete

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(
    new DeleteCommand<LedgerJournalHeader>(entity),
    cancellationToken);
// result.IsSuccess == true
```

### Batch create

```csharp
var lines = new List<LedgerJournalLine>
{
    new() { JournalBatchNumber = "JBN-001", LineNumber = 1, DataAreaId = "USMF",
            AccountDisplayValue = "600100", DebitAmount = 1000m },
    new() { JournalBatchNumber = "JBN-001", LineNumber = 2, DataAreaId = "USMF",
            AccountDisplayValue = "200100", CreditAmount = 1000m }
};

Result result = await mediator.Send(
    new CreateBatchCommand<LedgerJournalLine>(lines),
    cancellationToken);
// result.IsSuccess == true
```

### Error handling

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(entity),
    cancellationToken);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    Console.WriteLine($"[{error?.Type}] {error?.Code}: {error?.Message}");
    // Output: [Validation] Validation.Error: 'DataAreaId' must not be empty
}
```

## Understand the Logging Context

Single-entity commands delegate `GetLoggingContext()` to the entity, exposing all public properties to structured logging. Batch commands return the entity count:

```csharp
// Single: delegates to Entity.GetLoggingContext()
new CreateCommand<LedgerJournalHeader>(header).GetLoggingContext();
// { "JournalBatchNumber": "JBN-001", "DataAreaId": "USMF", "Description": "Vendor payments" }

// Batch: returns count
new CreateBatchCommand<LedgerJournalLine>(lines).GetLoggingContext();
// { "Count": 2 }
```

## See Also

- [[API-BaseEntity]] — base class for entities used in generic commands
- [[API-ICommand]] — command interface that generic commands implement
- [[API-Generic-Queries]] — corresponding generic query types
- [[API-Pipeline-Behaviours]] — behaviours that run before command handlers
