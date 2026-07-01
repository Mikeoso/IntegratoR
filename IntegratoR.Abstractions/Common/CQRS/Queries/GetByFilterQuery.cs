using System.Linq.Expressions;
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Queries;
using IntegratoR.Abstractions.Interfaces.Telemetry;

namespace IntegratoR.Abstractions.Common.CQRS.Queries;

/// <summary>
/// Represents a generic query to retrieve a collection of entities matching a filter expression.
/// </summary>
/// <typeparam name="TEntity">The type of the entity to query.</typeparam>
/// <param name="Filter">A LINQ expression tree that defines the criteria for filtering the entities.</param>
/// <remarks>The handler translates the LINQ expression into OData <c>$filter</c> syntax.</remarks>
public record GetByFilterQuery<TEntity>(Expression<Func<TEntity, bool>> Filter) : IQuery<Result<IEnumerable<TEntity>>> where TEntity : class
{
    /// <summary>
    /// Gets the structured logging context for this query.
    /// </summary>
    /// <returns>A context containing the entity type name and the filter expression.</returns>
    public virtual IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return new Dictionary<string, object>
        {
            { "EntityType", typeof(TEntity).Name },
            { "Filter", Filter.ToString() }
        };
    }
}
