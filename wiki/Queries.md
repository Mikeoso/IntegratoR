# Queries

```csharp
// Lookup by composite key
Result<LedgerJournalHeader> result = await mediator.Send(
    new GetByKeyQuery<LedgerJournalHeader>(["USMF", "JBN-001"]),
    cancellationToken);
// result.Value.JournalBatchNumber == "JBN-001"

// Filter-based search
Result<IEnumerable<LedgerJournalHeader>> headers = await mediator.Send(
    new GetByFilterQuery<LedgerJournalHeader>(
        h => h.DataAreaId == "USMF" && h.JournalName == "GenJrn"),
    cancellationToken);
// headers.Value contains matching entities
```

## IQuery\<TResponse\>

`public interface IQuery<out TResponse> : IRequest<TResponse>, IContext { }` -- marker interface for CQRS read operations. Queries are side-effect-free and flow through the MediatR pipeline (Logging -> Validation -> Caching -> Handler). Extends `IContext`, requiring `GetLoggingContext()` for structured logging.

```csharp
public record GetCustomerQuery(string CustomerId, string DataAreaId)
    : IQuery<Result<CustomerEntity>>
{
    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object>
        {
            { "CustomerId", CustomerId },
            { "DataAreaId", DataAreaId }
        };
}
```

For cacheable queries, implement [[Caching]] instead of `IQuery` directly.

## GetByKeyQuery\<TEntity\>

`public record GetByKeyQuery<TEntity>(object[] CompositeKey) : IQuery<Result<TEntity>> where TEntity : class, IEntity` -- retrieves a single entity by its composite key values. Key order must match the entity's `GetCompositeKey()` / `[Key]` attribute order.

```csharp
// Two-part key: [DataAreaId, JournalBatchNumber]
var query = new GetByKeyQuery<LedgerJournalHeader>(["USMF", "JBN-001"]);

// Three-part key: [DataAreaId, SalesOrderNumber, LineNumber]
var query = new GetByKeyQuery<SalesOrderLine>(["USMF", "SO-001", 1.0m]);
```

A key query that finds no match returns a failed `Result` with `ErrorType.NotFound`:

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(
    new GetByKeyQuery<LedgerJournalHeader>(["USMF", "DOES-NOT-EXIST"]),
    cancellationToken);

if (result.IsFailed)
{
    IntegrationError? error = result.GetError();
    // error.Type == ErrorType.NotFound
}
```

## GetByFilterQuery\<TEntity\>

`public record GetByFilterQuery<TEntity>(Expression<Func<TEntity, bool>> Filter) : IQuery<Result<IEnumerable<TEntity>>> where TEntity : class` -- retrieves entities matching a LINQ expression, translated to OData `$filter`.

```csharp
Result<IEnumerable<LedgerJournalHeader>> result = await mediator.Send(
    new GetByFilterQuery<LedgerJournalHeader>(
        j => j.DataAreaId == "USMF" && j.JournalName == "GenJrn"),
    cancellationToken);
// Translates to: $filter=dataAreaId eq 'USMF' and JournalName eq 'GenJrn'

foreach (LedgerJournalHeader header in result.Value)
    Console.WriteLine(header.JournalBatchNumber); // JBN-001, JBN-002, ...
```

No matches returns a successful `Result` with an empty collection (not a failure).

### Filter patterns

```csharp
j => j.DataAreaId == "USMF"                                    // equality
j => j.DataAreaId == "USMF" && j.JournalName == "GenJrn"       // multiple conditions
j => j.Description.Contains("accrual")                         // string contains
j => j.IsPosted == NoYes.Yes                                   // enum comparison
```

## Direct service calls

Bypass MediatR by injecting `IService<TEntity>` directly. The same operations are available without pipeline behaviours.

```csharp
// Get by key
Result<LedgerJournalHeader> result = await service.GetByKeyAsync(
    ["USMF", "00628"], cancellationToken);
// result.Value is the matching entity, or result.IsFailed with ErrorType.NotFound

// Find by filter
Result<IEnumerable<LedgerJournalHeader>> result = await service.FindAsync(
    j => j.DataAreaId == "USMF" && j.JournalName == "GenJrn", cancellationToken);
// Pass null to FindAsync to retrieve all entities (use with caution)
```

## Advanced queries with IODataService

Inject `IODataService<TEntity>` for paging, sorting, projection, and count operations.

### QueryAsync

```csharp
Result<IEnumerable<LedgerJournalHeader>> result = await oDataService.QueryAsync(
    filter: h => h.DataAreaId == "USMF",
    orderBy: q => q.OrderByDescending(h => h.JournalBatchNumber),
    top: 50,
    skip: 0,
    cancellationToken: cancellationToken);
// Up to 50 entities sorted by batch number descending
```

| Parameter | OData Equivalent | Description |
|-----------|-----------------|-------------|
| `filter` | `$filter` | LINQ expression for filtering |
| `orderBy` | `$orderby` | Sorting function |
| `expand` | `$expand` | Include navigation properties |
| `select` | `$select` | Return only specific fields |
| `skip` | `$skip` | Records to skip (paging) |
| `top` | `$top` | Maximum records to return |

### CountAsync

Server-side count without transferring entities:

```csharp
Result<int> count = await oDataService.CountAsync(
    h => h.DataAreaId == "USMF" && h.IsPosted == NoYes.No, cancellationToken);
// count.Value == 42
```

## Custom query with handler

Implement `IQuery<TResponse>` for domain-specific queries that go beyond generic key/filter lookups:

```csharp
public record GetOpenOrdersQuery(string DataAreaId)
    : IQuery<Result<IEnumerable<SalesOrderEntity>>>
{
    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object> { { "DataAreaId", DataAreaId } };
}

public class GetOpenOrdersHandler
    : IRequestHandler<GetOpenOrdersQuery, Result<IEnumerable<SalesOrderEntity>>>
{
    private readonly IService<SalesOrderEntity> _service;

    public GetOpenOrdersHandler(IService<SalesOrderEntity> service) => _service = service;

    public async Task<Result<IEnumerable<SalesOrderEntity>>> Handle(
        GetOpenOrdersQuery request, CancellationToken cancellationToken)
    {
        return await _service.FindAsync(
            o => o.DataAreaId == request.DataAreaId && o.Status == OrderStatus.Open,
            cancellationToken).ConfigureAwait(false);
    }
}

// Send it
Result<IEnumerable<SalesOrderEntity>> result = await mediator.Send(
    new GetOpenOrdersQuery("USMF"), cancellationToken);
```

## Result handling

All query methods return `Result<T>`. Use `IsSuccess`, `IsFailed`, `GetError()`, or `Match`:

```csharp
string output = result.Match(
    onSuccess: header => $"Found: {header.JournalBatchNumber}",
    onFailure: error => $"Error: [{error.Code}] {error.Message}");
```
