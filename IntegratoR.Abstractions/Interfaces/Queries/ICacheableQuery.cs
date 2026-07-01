using IntegratoR.Abstractions.Interfaces.Queries;

namespace IntegratoR.Abstractions.Interfaces.Queries;

/// <summary>
/// Defines a CQRS query whose response can be cached by the caching pipeline behaviour.
/// </summary>
/// <typeparam name="TResponse">The type of the response to be cached.</typeparam>
public interface ICacheableQuery<TResponse> : IQuery<TResponse>
{
    /// <summary>
    /// Gets the unique key used to store and retrieve the query's response from the cache.
    /// </summary>
    string CacheKey { get; }

    /// <summary>
    /// Gets the duration for which the query's response should be cached.
    /// </summary>
    /// <remarks>A <see langword="null"/> value bypasses the cache for this query instance.</remarks>
    TimeSpan? CacheDuration { get; }

    /// <summary>
    /// Gets the values that uniquely define this query instance for caching purposes.
    /// </summary>
    /// <returns>An array of objects used to generate the cache key.</returns>
    [Obsolete("since v1.4.0; CachingBehaviour uses CacheKey directly; removed next MAJOR")]
    object[] GetCacheKeyValues();

    /// <summary>
    /// Generates a stable cache key string from the query's defining values.
    /// </summary>
    /// <returns>The generated cache key.</returns>
    [Obsolete("since v1.4.0; CachingBehaviour uses CacheKey directly; removed next MAJOR")]
    string GenerateCacheKey();
}
