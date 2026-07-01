namespace IntegratoR.Abstractions.Interfaces.Services;

/// <summary>
/// Defines a generic application caching service, abstracting the underlying caching technology.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Asynchronously retrieves an item from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the object to retrieve.</typeparam>
    /// <param name="cacheKey">The unique key identifying the cached item.</param>
    /// <returns>The deserialised object if found; otherwise, <see langword="default"/>.</returns>
    Task<T?> GetAsync<T>(string cacheKey);

    /// <summary>
    /// Asynchronously stores an item in the cache, overwriting any existing item with the same key.
    /// </summary>
    /// <typeparam name="T">The type of the object to store.</typeparam>
    /// <param name="cacheKey">The unique key to associate with the item.</param>
    /// <param name="value">The object to store in the cache.</param>
    /// <param name="expirationTime">The duration after which the item expires; if omitted, the implementation's default policy applies.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetAsync<T>(string cacheKey, T value, TimeSpan? expirationTime = null);

    /// <summary>
    /// Asynchronously removes an item from the cache.
    /// </summary>
    /// <param name="cacheKey">The unique key of the item to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RemoveAsync(string cacheKey);
}
