using System.Collections.Generic;
using System.Text.Json;
using FluentResults;
using IntegratoR.Abstractions.Interfaces;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Queries;

namespace IntegratoR.Abstractions.Common.CQRS.Queries;

/// <summary>
/// Represents a generic query to retrieve a single entity by its key, supporting both simple and composite keys.
/// </summary>
/// <typeparam name="TEntity">The type of the entity to query.</typeparam>
/// <param name="CompositeKey">The ordered key segment values that identify the entity, matching D365 F&amp;O composite-key ordering.</param>
/// <remarks>The handler uses <paramref name="CompositeKey"/> to construct the key segment of the OData URL.</remarks>
public record GetByKeyQuery<TEntity>(object[] CompositeKey) : IQuery<Result<TEntity>> where TEntity : class, IEntity
{
    /// <summary>
    /// Gets the structured logging context for this query.
    /// </summary>
    /// <returns>A context containing the entity type name and the serialised key values.</returns>
    public IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return new Dictionary<string, object>
        {
            { "EntityType", typeof(TEntity).Name },
            { "KeyValues", CompositeKey is not null ? JsonSerializer.Serialize(CompositeKey) : "null" }
        };
    }
}
