# Commands

```csharp
var journal = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalName = "GenJrn",
    Description = "Monthly accruals - March 2026"
};

Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(journal), cancellationToken);
// result.Value.JournalBatchNumber == "00628" (server-generated)
```

Commands are CQRS write operations sent through the MediatR pipeline (Logging -> Validation -> Caching -> Handler). All commands return `FluentResults.Result` types -- never exceptions for business logic errors.

## ICommand\<TResponse\>

`public interface ICommand<out TResponse> : IRequest<TResponse>, IContext { }` -- a command that returns a response payload. The non-generic `ICommand : IRequest<Result>, IContext { }` variant returns only success/failure. Both require `GetLoggingContext()` for structured logging.

```csharp
// Command with response value
public record PostInvoiceCommand(string InvoiceId) : ICommand<Result<string>>
{
    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object> { { "InvoiceId", InvoiceId } };
}

// Fire-and-forget command
public record TriggerSyncCommand(string DataAreaId) : ICommand
{
    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object> { { "DataAreaId", DataAreaId } };
}
```

For standard CRUD, use the generic commands below instead of writing custom commands.

## Generic Commands

Six pre-built command records for any [[Entities|IEntity]] type. Single-entity commands return `Result<TEntity>`, batch commands return `Result`.

```csharp
public record CreateCommand<TEntity>(TEntity Entity) : ICommand<Result<TEntity>> where TEntity : IEntity
public record UpdateCommand<TEntity>(TEntity Entity) : ICommand<Result<TEntity>> where TEntity : IEntity
public record DeleteCommand<TEntity>(TEntity Entity) : ICommand<Result<TEntity>> where TEntity : IEntity

public record CreateBatchCommand<TEntity>(IEnumerable<TEntity> Entities) : ICommand<Result> where TEntity : IEntity
public record UpdateBatchCommand<TEntity>(IEnumerable<TEntity> Entities) : ICommand<Result> where TEntity : IEntity
public record DeleteBatchCommand<TEntity>(IEnumerable<TEntity> Entities) : ICommand<Result> where TEntity : IEntity
```

### Create

```csharp
var header = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalName = "GenJrn",
    Description = "Monthly accruals"
};

Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(header), cancellationToken);
// result.Value.JournalBatchNumber is now populated by D365 number sequence
```

Properties marked `[ODataField(IgnoreOnCreate = true)]` are excluded from the POST payload -- use this for server-generated fields like `JournalBatchNumber`.

### Update

```csharp
var header = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalBatchNumber = "00628",       // all [Key] properties required
    JournalName = "GenJrn",
    Description = "Monthly accruals (amended)"
};

Result<LedgerJournalHeader> result = await mediator.Send(
    new UpdateCommand<LedgerJournalHeader>(header), cancellationToken);
// result.Value.Description == "Monthly accruals (amended)"
```

The OData client sends PATCH to `LedgerJournalHeaders(dataAreaId='USMF',JournalBatchNumber='00628')`. Properties marked `[ODataField(IgnoreOnUpdate = true)]` are excluded automatically.

### Delete

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(
    new DeleteCommand<LedgerJournalHeader>(header), cancellationToken);
// result.IsSuccess == true
```

D365 F&O treats deleting a non-existent entity as success (HTTP 204), making deletes naturally idempotent.

### Batch

```csharp
var journals = new List<LedgerJournalHeader>
{
    new() { DataAreaId = "USMF", JournalName = "GenJrn", Description = "Batch journal 1" },
    new() { DataAreaId = "USMF", JournalName = "GenJrn", Description = "Batch journal 2" },
    new() { DataAreaId = "USMF", JournalName = "GenJrn", Description = "Batch journal 3" }
};

Result result = await mediator.Send(
    new CreateBatchCommand<LedgerJournalHeader>(journals), cancellationToken);
// result.IsSuccess == true (atomic, all-or-nothing via OData $batch)
```

Batch commands return non-generic `Result` — server-generated values (e.g. `JournalBatchNumber`) are not returned. [[Queries|Query them]] after the batch completes. `UpdateBatchCommand<T>` and `DeleteBatchCommand<T>` follow the same pattern. See [[Batch-Operations]] for chunking and advanced patterns.

## IService\<TEntity\>

`public interface IService<TEntity> where TEntity : IEntity` -- abstracts CRUD and query operations. The generic command handlers call these methods internally; inject `IService<T>` directly when you need to bypass the MediatR pipeline.

```csharp
// Inject via DI
IService<LedgerJournalHeader> service = ...;

Result<LedgerJournalHeader> created = await service.AddAsync(header, cancellationToken);
// Result<TEntity> -- success with server-generated fields

Result<LedgerJournalHeader> updated = await service.UpdateAsync(header, cancellationToken);
// Result<TEntity> -- success with updated entity

Result deleted = await service.DeleteAsync(header, cancellationToken);
// Result (non-generic) -- success/failure only

Result<LedgerJournalHeader> fetched = await service.GetByKeyAsync(
    ["USMF", "JBN-001"], cancellationToken);
// Result<TEntity> -- entity or NotFound failure

Result<IEnumerable<LedgerJournalHeader>> found = await service.FindAsync(
    h => h.DataAreaId == "USMF", cancellationToken);
// Result<IEnumerable<TEntity>> -- matching entities or empty collection
```

## ODataService\<TEntity\>

The concrete `IService<T>` implementation, registered by `services.AddODataClient(configuration)`. Implements `IService<TEntity>`, `IODataService<TEntity>`, and `IODataBatchService<TEntity>`.

CRUD methods map directly to OData HTTP verbs:

| Method | HTTP | Payload Rules |
|--------|------|--------------|
| `AddAsync` | POST | Excludes `[ODataField(IgnoreOnCreate = true)]`, `[NotMapped]`, `[JsonIgnore]`, default values |
| `UpdateAsync` | PATCH | Excludes `[ODataField(IgnoreOnUpdate = true)]`, `[NotMapped]`, `[JsonIgnore]`, default values |
| `DeleteAsync` | DELETE | Requires populated composite key; treats NotFound as success |
| `GetByKeyAsync` | GET | Key values in `[Key]` attribute order |
| `FindAsync` | GET + `$filter` | LINQ expression translated to OData filter |

The entity set name resolves from `[Table("LedgerJournalHeaders")]` on the entity class, falling back to pluralised type name. Composite keys map `[Key]` properties to the OData URL segment.

Batch methods on `IODataBatchService<TEntity>` (`AddBatchAsync`, `UpdateBatchAsync`, `DeleteBatchAsync`) are atomic via OData `$batch`.

## ODataFieldAttribute

Controls per-operation property serialisation:

```csharp
[Key]
[JsonPropertyName("JournalBatchNumber")]
[ODataField(IgnoreOnCreate = true)]      // excluded from POST, D365 assigns via number sequence
public string? JournalBatchNumber { get; set; }
```

| Property | Effect |
|----------|--------|
| `IgnoreOnCreate = true` | Excluded from POST payload |
| `IgnoreOnUpdate = true` | Excluded from PATCH payload |

## Error Handling

All commands and service methods return typed errors via `IntegrationError`:

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(invalid), cancellationToken);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    // error.Code    -> "Validation.Error"
    // error.Message -> "'Journal Name' must not be empty."
    // error.Type    -> ErrorType.Validation
}

string message = result.Match(
    onSuccess: entity => $"Created journal {entity.JournalBatchNumber}",
    onFailure: error => $"Failed: [{error.Code}] {error.Message}");
```

`ErrorType` values: `Failure`, `Validation`, `NotFound`, `Conflict`. See [[Error-Handling]] for more detail.

## See Also

- [[Entities]] — define entities with `BaseEntity<TKey>` and `ODataFieldAttribute`
- [[Queries]] — query by composite key or filter expression
- [[Batch-Operations]] — bulk create, update, and delete with chunking
- [[Validation]] — FluentValidation in the MediatR pipeline
- [[Error-Handling]] — `Result<T>` pattern and `IntegrationError`
