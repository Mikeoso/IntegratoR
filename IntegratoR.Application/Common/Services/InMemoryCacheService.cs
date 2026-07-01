using IntegratoR.Abstractions.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;

namespace IntegratoR.Application.Common.Services;

/// <summary>
/// Provides a thread-safe, in-memory <see cref="ICacheService"/> backed by <see cref="IMemoryCache"/>.
/// </summary>
/// <remarks>Suitable for single-instance deployments; access is serialised through a <see cref="SemaphoreSlim"/>. Not suitable for scaled-out, multi-instance environments, where <see cref="DistributedCacheService"/> should be used instead.</remarks>
public class InMemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryCacheService"/> class.
    /// </summary>
    /// <param name="cache">The memory cache provided by the DI container.</param>
    public InMemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    /// <remarks>This operation is thread-safe.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="cacheKey"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public async Task<T?> GetAsync<T>(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            throw new ArgumentNullException(nameof(cacheKey), "Cache key cannot be null or empty.");
        }

        await _cacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return _cache.TryGetValue(cacheKey, out T? value) ? value : default;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>This operation is thread-safe.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="cacheKey"/> is <see langword="null"/>, empty, or whitespace, or <paramref name="value"/> is <see langword="null"/>.</exception>
    public async Task SetAsync<T>(string cacheKey, T value, TimeSpan? expirationTime = null)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            throw new ArgumentNullException(nameof(cacheKey), "Cache key cannot be null or empty.");
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value), "Cannot cache a null value.");
        }

        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            // The 30-minute default is a sensible starting point, but should be overridden with values
            // appropriate for the volatility of the data being cached.
            AbsoluteExpirationRelativeToNow = expirationTime ?? TimeSpan.FromMinutes(30)
        };

        await _cacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _cache.Set(cacheKey, value, cacheEntryOptions);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>This operation is thread-safe.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="cacheKey"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public async Task RemoveAsync(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            throw new ArgumentNullException(nameof(cacheKey), "Cache key cannot be null or empty.");
        }

        await _cacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _cache.Remove(cacheKey);
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}
