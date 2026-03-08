# Generic Queries

Two pre-built query records for standard data retrieval: lookup by composite key and filter-based search.

## Use the Generic Queries

```csharp
// Lookup by composite key
Result<LedgerJournalHeader> result = await mediator.Send(
    new GetByKeyQuery<LedgerJournalHeader>(["USMF", "JBN-001"]),
    cancellationToken);
// result.Value.JournalBatchNumber == "JBN-001"

// Filter-based search
Result<IEnumerable<LedgerJournalHeader>> result = await mediator.Send(
    new GetByFilterQuery<LedgerJournalHeader>(
        h => h.DataAreaId == "USMF" && h.Description.Contains("accrual")),
    cancellationToken);
// result.Value contains matching headers
```

## GetByKeyQuery\<TEntity\>

Retrieves a single entity by its composite key values.

```csharp
public record GetByKeyQuery<TEntity>(object[] CompositeKey)
    : IQuery<Result<TEntity>>
    where TEntity : class, IEntity
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `CompositeKey` | `object[]` | Yes | Key values in the same order as the entity's `GetCompositeKey()` |

### Composite key construction

The key array must match the order defined in the entity's `GetCompositeKey()` override:

```csharp
// Entity defines: GetCompositeKey() => [DataAreaId, JournalBatchNumber]
// Query must match that order:
var query = new GetByKeyQuery<LedgerJournalHeader>(["USMF", "JBN-001"]);

// For a three-part key:
// Entity defines: GetCompositeKey() => [DataAreaId, SalesOrderNumber, LineNumber]
var query = new GetByKeyQuery<SalesOrderLine>(["USMF", "SO-001", 1.0m]);
```

### Logging context

```csharp
query.GetLoggingContext();
// { "EntityType": "LedgerJournalHeader", "KeyValues": "[\"USMF\",\"JBN-001\"]" }
```

## GetByFilterQuery\<TEntity\>

Retrieves a collection of entities matching a LINQ filter expression, translated to an OData `$filter` query.

```csharp
public record GetByFilterQuery<TEntity>(Expression<Func<TEntity, bool>> Filter)
    : IQuery<Result<IEnumerable<TEntity>>>
    where TEntity : class
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Filter` | `Expression<Func<TEntity, bool>>` | Yes | A LINQ expression tree defining the filter criteria |

### Logging context

```csharp
var query = new GetByFilterQuery<LedgerJournalHeader>(
    h => h.DataAreaId == "USMF");

query.GetLoggingContext();
// { "EntityType": "LedgerJournalHeader", "Filter": "h => (h.DataAreaId == \"USMF\")" }
```

## See Examples

### Lookup by key

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(
    new GetByKeyQuery<LedgerJournalHeader>(["USMF", "JBN-001"]),
    cancellationToken);

if (result.IsSuccess)
    Console.WriteLine($"Found: {result.Value.Description}");
// Output: Found: Monthly accruals
```

### Filter with multiple conditions

```csharp
Result<IEnumerable<LedgerJournalHeader>> result = await mediator.Send(
    new GetByFilterQuery<LedgerJournalHeader>(
        h => h.DataAreaId == "USMF" && h.JournalBatchNumber.StartsWith("JBN")),
    cancellationToken);

foreach (LedgerJournalHeader header in result.Value)
    Console.WriteLine(header.JournalBatchNumber);
// Output:
// JBN-001
// JBN-002
```

### Error handling

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(
    new GetByKeyQuery<LedgerJournalHeader>(["USMF", "DOES-NOT-EXIST"]),
    cancellationToken);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    Console.WriteLine($"[{error?.Type}] {error?.Message}");
    // Output: [NotFound] Entity not found for key: DOES-NOT-EXIST
}
```

## See Also

- [[API-Generic-Commands]] — corresponding generic command types
- [[API-BaseEntity]] — base class for entities returned by queries
- [[API-IQuery]] — query interface that generic queries implement
- [[API-ICacheableQuery]] — add caching to query results
