namespace IntegratoR.Abstractions.Interfaces.Services;

/// <summary>
/// Defines a generic contract for an application caching service, abstracting the underlying
/// caching technology and providing a simple interface for cache operations.
/// </summary>
/// <remarks>
/// This interface is a critical component for performance optimization. It decouples the application's
/// business logic (e.g., a MediatR caching pipeline behavior) from the concrete cache implementation.
///
/// In a typical Azure-hosted environment, the implementation of this interface would be a wrapper
/// around a distributed cache like **Azure Cache for Redis**. This ensures that the cache is
/// accessible and consistent across all instances of a scaled-out service, such as an Azure Function App.
/// </remarks>
public interface ICacheService
{
    /// <summary>
    /// Asynchronously retrieves an item from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the object to retrieve.</typeparam>
    /// <param name="cacheKey">The unique key identifying the cached item.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that represents the asynchronous operation.
    /// The task result contains the deserialized object if found, or <c>default(T)</c> if the key does not exist.
    /// </returns>
    /// <remarks>
    /// The implementation is responsible for handling the deserialization of the cached data
    /// (e.g., from a JSON string) into an object of type <typeparamref name="T"/>.
    /// </remarks>
    Task<T?> GetAsync<T>(string cacheKey);

    /// <summary>
    /// Asynchronously stores an item in the cache with a specified key and optional expiration.
    /// If an item with the same key already exists, it will be overwritten.
    /// </summary>
    /// <typeparam name="T">The type of the object to store.</typeparam>
    /// <param name="cacheKey">The unique key to associate with the item.</param>
    /// <param name="value">The object to store in the cache.</param>
    /// <param name="expirationTime">
    /// The <see cref="TimeSpan"/> after which the item should expire. If not provided, a default
    /// expiration policy (e.g., 30 minutes) should be applied by the implementation.
    /// </param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>
    /// The implementation is responsible for serializing the <paramref name="value"/> into a suitable format
    /// for storage in the underlying cache provider (e.g., a JSON string for Redis).
    /// </remarks>
    Task SetAsync<T>(string cacheKey, T value, TimeSpan? expirationTime = null);

    /// <summary>
    /// Asynchronously removes an item from the cache.
    /// </summary>
    /// <param name="cacheKey">The unique key of the item to remove.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method is essential for cache invalidation strategies. For example, after a command
    /// successfully updates a resource in D365 F&O, its handler should call this method to
    /// evict the corresponding stale data from the cache, ensuring subsequent queries fetch fresh data.
    /// </remarks>
    Task RemoveAsync(string cacheKey);
}