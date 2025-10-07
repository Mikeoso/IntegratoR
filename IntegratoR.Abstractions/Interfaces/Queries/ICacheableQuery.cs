using IntegratoR.Abstractions.Interfaces.Queries;
using IntegratoR.Abstractions.Interfaces.Results;

namespace IntegratoR.Abstractions.Interfaces.Queries;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file defines the contract for implementing a caching decorator for CQRS queries.
// Caching is a critical performance optimization in D365 F&O integrations, especially for data
// that is read frequently but changes infrequently (e.g., configuration, parameters, financial dimensions).
// By abstracting the caching logic, we can apply it consistently across the application.
// </remarks>
// ---------------------------------------------------------------------------------------------

/// <summary>
/// Defines a contract for a CQRS query whose response can be cached, improving performance
/// and reducing load on the D365 F&O OData endpoint.
/// </summary>
/// <typeparam name="TResponse">The type of the response to be cached.</typeparam>
/// <remarks>
/// This interface is designed to be used with a MediatR pipeline behavior. The behavior intercepts
/// any request implementing <c>ICacheableQuery</c>, checks a distributed cache (like Redis) for an
/// entry matching the <see cref="CacheKey"/>, and either returns the cached response or executes
/// the query handler and caches the result for the specified <see cref="CacheDuration"/>.
/// </remarks>
public interface ICacheableQuery<TResponse> : IQuery<TResponse> where TResponse : IResult
{
    /// <summary>
    /// Gets the unique key used to store and retrieve the query's response from the cache.
    /// </summary>
    /// <remarks>
    /// This is typically implemented as a get-only property that calls <see cref="GenerateCacheKey"/>
    /// to ensure the key is always derived consistently from the query's parameters.
    /// </remarks>
    string CacheKey { get; }

    /// <summary>
    /// Gets the duration for which the query's response should be cached.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> value indicates that this specific query instance should not be cached,
    /// providing a mechanism to bypass the cache dynamically when fresh data is required.
    /// </remarks>
    TimeSpan? CacheDuration { get; }

    /// <summary>
    /// Gets the collection of values that uniquely define this query instance for caching purposes.
    /// </summary>
    /// <returns>An array of objects that will be used to generate the cache key.</returns>
    /// <remarks>
    /// This method is crucial for cache correctness. It forces the developer to explicitly select
    /// the properties of the query that affect its result (e.g., an entity ID, a filter string,
    /// a company code), ensuring that two different queries produce two different cache keys.
    /// </remarks>
    object[] GetCacheKeyValues();

    /// <summary>
    /// Generates a unique and stable cache key string from the query's defining values.
    /// </summary>
    /// <returns>A unique string to be used as the cache key.</returns>
    /// <remarks>
    /// A recommended implementation is to combine a static prefix (like the query name) with a
    /// serialized (e.g., JSON) representation of the objects from <see cref="GetCacheKeyValues"/>.
    /// This creates a key that is both unique and human-readable for debugging purposes.
    /// Example: "CustomerById:[\"C-123\",\"USMF\"]".
    /// </remarks>
    string GenerateCacheKey();
}