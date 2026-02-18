using IntegratoR.Abstractions.Interfaces.Services;

namespace IntegratoR.TestKit.Fakes;

/// <summary>
/// An in-memory implementation of <see cref="ICacheService"/> for use in unit tests.
/// Provides test-helper members (<see cref="Contains"/>, <see cref="Count"/>, <see cref="Clear"/>)
/// to inspect and reset cache state between tests.
/// </summary>
public sealed class FakeCacheService : ICacheService
{
    private readonly Dictionary<string, object?> _store = new();

    /// <summary>
    /// Gets the number of entries currently in the cache.
    /// </summary>
    public int Count => _store.Count;

    /// <summary>
    /// Returns <c>true</c> if the cache contains an entry for the given <paramref name="cacheKey"/>.
    /// </summary>
    /// <param name="cacheKey">The key to check.</param>
    /// <returns><c>true</c> if the key exists; otherwise <c>false</c>.</returns>
    public bool Contains(string cacheKey) => _store.ContainsKey(cacheKey);

    /// <summary>
    /// Removes all entries from the cache.
    /// </summary>
    public void Clear() => _store.Clear();

    /// <inheritdoc/>
    public Task<T?> GetAsync<T>(string cacheKey)
    {
        if (_store.TryGetValue(cacheKey, out var value))
            return Task.FromResult(value is T typed ? typed : default);

        return Task.FromResult(default(T?));
    }

    /// <inheritdoc/>
    public Task SetAsync<T>(string cacheKey, T value, TimeSpan? expirationTime = null)
    {
        _store[cacheKey] = value;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string cacheKey)
    {
        _store.Remove(cacheKey);
        return Task.CompletedTask;
    }
}
