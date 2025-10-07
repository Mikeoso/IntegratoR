using IntegratoR.Abstractions.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;

namespace IntegratoR.Application.Common.Services;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file provides a concrete, in-memory implementation of the ICacheService contract.
// It serves as a foundational caching mechanism for single-instance environments.
// </remarks>
// ---------------------------------------------------------------------------------------------

/// <summary>
/// A thread-safe, in-memory implementation of the <see cref="ICacheService"/> interface,
/// utilizing the standard <see cref="IMemoryCache"/>.
/// </summary>
/// <remarks>
/// This service is ideal for local development, testing, or simple, single-instance production
/// deployments. It provides a fast and simple caching solution without external dependencies.
///
/// <para><b>IMPORTANT ARCHITECTURAL NOTE:</b></para>
/// This implementation is **not suitable for scaled-out, multi-instance environments** (e.g., a
/// production Azure Function App on a Consumption or Premium plan). Each application instance
/// will have its own private memory, leading to an inconsistent and ineffective cache. For such
/// scenarios, a distributed cache implementation (e.g., using Azure Cache for Redis) is required.
///
/// To ensure thread safety in concurrent scenarios, all access to the underlying cache is
/// controlled by a <see cref="SemaphoreSlim"/>.
/// </remarks>
public class InMemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryCacheService"/> class.
    /// </summary>
    /// <param name="cache">The <see cref="IMemoryCache"/> instance provided by the DI container.</param>
    public InMemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    /// <remarks>This operation is thread-safe.</remarks>
    public async Task<T?> GetAsync<T>(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            throw new ArgumentNullException(nameof(cacheKey), "Cache key cannot be null or empty.");
        }

        await _cacheLock.WaitAsync();
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

        await _cacheLock.WaitAsync();
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
    public async Task RemoveAsync(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            throw new ArgumentNullException(nameof(cacheKey), "Cache key cannot be null or empty.");
        }

        await _cacheLock.WaitAsync();
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