using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Queries;
using System.Linq.Expressions;

namespace IntegratoR.Abstractions.Common.CQRS;

/// <summary>
/// Represents a generic query within a CQRS pattern to retrieve a collection of entities
/// based on a specified filter expression.
/// </summary>
/// <typeparam name="TEntity">The type of the entity to query. This is typically a client-side proxy class corresponding to a domain object.</typeparam>
/// <param name="Filter">A LINQ expression tree that defines the criteria for filtering the entities.</param>
/// <remarks>
/// This query is designed to be highly reusable and type-safe across different entity types.
/// The handler for this query is responsible for translating the provided LINQ expression
/// into the appropriate OData filter syntax for a request to the OData endpoint.
/// This approach decouples the business logic
/// from the data access implementation and keeps filtering logic strongly typed.
/// </remarks>
public record GetByFilterQuery<TEntity>(Expression<Func<TEntity, bool>> Filter) : IQuery<Result<IEnumerable<TEntity>>> where TEntity : class
{
    public IReadOnlyDictionary<string, object> GetContextForLogging()
    {
        return new Dictionary<string, object>
        {
            { "EntityType", typeof(TEntity).Name },
            { "Filter", Filter.ToString() }
        };
    }
}
