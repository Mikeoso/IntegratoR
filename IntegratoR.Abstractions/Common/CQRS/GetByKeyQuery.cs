using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Queries;
using System.Collections.Generic;
using System.Text.Json;

namespace IntegratoR.Abstractions.Common.CQRS;

/// <summary>
/// Represents a generic query within a CQRS pattern to retrieve a single entity using its primary key.
/// This query is specifically designed to handle both simple and composite keys.
/// </summary>
/// <typeparam name="TEntity">The type of the entity to query, which must be a class implementing <see cref="IEntity{TKey}"/>.</typeparam>
/// <typeparam name="TKey">The data type of the entity's primary key as defined by the <see cref="IEntity{TKey}"/> interface.</typeparam>
/// <param name="keyValues">An object representing the primary key(s) of the entity to retrieve.</param>
/// <remarks>
/// This query provides a flexible mechanism for fetching entities by key, which is crucial for
/// OData that often feature composite primary keys.
///
/// <para><b>Usage Examples:</b></para>
/// <list type="bullet">
///   <item>
///     <description><b>For a simple key:</b> Pass the key value directly. E.g., `new GetByKeyQuery&lt;Customer, string&gt;("CUST-001")`.</description>
///   </item>
///   <item>
///     <description><b>For a composite key:</b> Pass an anonymous object or an <see cref="IDictionary{TKey, TValue}"/> where property names match the key field names of the OData entity.
///     E.g., `new GetByKeyQuery&lt;SalesOrderLine, long&gt;(new { SalesOrderNumber = "SO-123", LineNumber = 1.0m })`.</description>
///   </item>
/// </list>
///
/// A query handler will use this <paramref name="keyValues"/> object to construct the key segment of an OData URL,
/// which the underlying implementation can do directly from an anonymous type.
/// </remarks>
public record GetByKeyQuery<TEntity, TKey>(object keyValues) : IQuery<Result<TEntity>> where TEntity : class, IEntity<TKey>
{
    public IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return new Dictionary<string, object>
        {
            { "EntityType", typeof(TEntity).Name },
            { "KeyValues", keyValues is not null ? JsonSerializer.Serialize(keyValues) : "null" }
        };
    }
}
