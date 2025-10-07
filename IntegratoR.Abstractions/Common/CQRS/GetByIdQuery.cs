using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Queries;

namespace IntegratoR.Abstractions.Common.CQRS;

/// <summary>
/// Represents a generic query within a CQRS pattern to retrieve a single entity by its unique primary key.
/// </summary>
/// <typeparam name="TEntity">The type of the entity to query, which must be a class implementing <see cref="IEntity{TKey}"/>.</typeparam>
/// <typeparam name="TKey">The data type of the entity's primary key (e.g., <see cref="long"/>, <see cref="string"/>, <see cref="Guid"/>).</typeparam>
/// <param name="Id">The primary key value of the entity to retrieve.</param>
/// <remarks>
/// This query provides a standardized, type-safe way to request an entity by its identifier.
/// A corresponding query handler is responsible for translating this request into a specific data access call.
/// In a OData integration context, this would typically involve constructing an OData key-based request,
/// such as `GET /data/MyDataEntity(keyValue)`, where <paramref name="Id"/> provides the `keyValue`.
/// This abstraction simplifies the calling code and decouples it from the underlying OData protocol details.
/// </remarks>
public record GetByIdQuery<TEntity, TKey>(TKey Id) : IQuery<Result<TEntity>> where TEntity : class, IEntity<TKey>
{
    public IReadOnlyDictionary<string, object> GetContextForLogging()
    {
        return new Dictionary<string, object>
        {
            { "EntityType", typeof(TEntity).Name },
            { "Id", Id?.ToString() ?? "null" }
        };
    }
}
