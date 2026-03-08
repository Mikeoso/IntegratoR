# IQuery

Marker interface for CQRS read operations. Queries are side-effect-free and flow through the MediatR pipeline, where they can be cached, validated, and logged.

## Use the Interface

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

## Interface

```csharp
public interface IQuery<out TResponse> : IRequest<TResponse>, IContext { }
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `TResponse` | Type parameter | The response type, typically `Result<T>` or `Result<IEnumerable<T>>` |

## See Examples

### Single-entity query

```csharp
public record GetVendorByAccountQuery(string VendorAccount, string DataAreaId)
    : IQuery<Result<VendorEntity>>
{
    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object>
        {
            { "VendorAccount", VendorAccount },
            { "DataAreaId", DataAreaId }
        };
}

// Handler
public class GetVendorByAccountHandler
    : IRequestHandler<GetVendorByAccountQuery, Result<VendorEntity>>
{
    private readonly IService<VendorEntity> _service;

    public GetVendorByAccountHandler(IService<VendorEntity> service) => _service = service;

    public async Task<Result<VendorEntity>> Handle(
        GetVendorByAccountQuery request, CancellationToken cancellationToken)
    {
        return await _service.GetByKeyAsync(
            [request.VendorAccount, request.DataAreaId], cancellationToken)
            .ConfigureAwait(false);
    }
}

// Sending
Result<VendorEntity> result = await mediator.Send(
    new GetVendorByAccountQuery("V-1001", "USMF"),
    cancellationToken);
// result.Value.VendorAccount == "V-1001"
```

### Collection query

```csharp
public record GetOpenOrdersQuery(string DataAreaId)
    : IQuery<Result<IEnumerable<SalesOrderEntity>>>
{
    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object> { { "DataAreaId", DataAreaId } };
}

// Sending
Result<IEnumerable<SalesOrderEntity>> result = await mediator.Send(
    new GetOpenOrdersQuery("USMF"),
    cancellationToken);
// result.Value contains matching orders
```

### Error handling

```csharp
Result<VendorEntity> result = await mediator.Send(query, cancellationToken);

result.Match(
    vendor => Console.WriteLine($"Found: {vendor.VendorAccount}"),
    error => Console.WriteLine($"[{error.Code}] {error.Message}")
);
// Output on failure: [OData.NotFound] Entity not found for key: V-9999
```

## Keep in Mind

- Queries extend `IContext`, requiring `GetLoggingContext()` for structured logging.
- Queries should be idempotent and side-effect-free.
- For cacheable queries, implement [[API-ICacheableQuery]] instead of `IQuery` directly.
- Use the pre-built [[API-Generic-Queries]] (`GetByKeyQuery<T>`, `GetByFilterQuery<T>`) for standard lookups.

## See Also

- [[API-ICommand]] — companion interface for write operations
- [[API-Generic-Queries]] — built-in query implementations
- [[API-ICacheableQuery]] — extend queries with caching support
- [[API-Pipeline-Behaviours]] — behaviours that intercept queries
