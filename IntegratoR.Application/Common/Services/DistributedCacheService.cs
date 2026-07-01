using System.Text.Json;
using IntegratoR.Abstractions.Common.Results.SystemText;
using IntegratoR.Abstractions.Interfaces.Services;
using Microsoft.Extensions.Caching.Distributed;

namespace IntegratoR.Application.Common.Services;

/// <summary>
/// Provides an <see cref="ICacheService"/> backed by <see cref="IDistributedCache"/> for scaled-out, multi-instance environments.
/// </summary>
/// <remarks>The underlying provider is configured via DI, typically Azure Cache for Redis; register this implementation instead of <see cref="InMemoryCacheService"/> when cache consistency across instances is required.</remarks>
public class DistributedCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    }.AddResultConverters();

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCacheService"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache provided by the DI container.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cache"/> is <see langword="null"/>.</exception>
    public DistributedCacheService(IDistributedCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="cacheKey"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public async Task<T?> GetAsync<T>(string cacheKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);

        var bytes = await _cache.GetAsync(cacheKey).ConfigureAwait(false);

        if (bytes is null || bytes.Length == 0)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(bytes, SerializerOptions);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="cacheKey"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public async Task SetAsync<T>(string cacheKey, T value, TimeSpan? expirationTime = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentNullException.ThrowIfNull(value);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expirationTime ?? TimeSpan.FromMinutes(30)
        };

        await _cache.SetAsync(cacheKey, bytes, options).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="cacheKey"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public async Task RemoveAsync(string cacheKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);

        await _cache.RemoveAsync(cacheKey).ConfigureAwait(false);
    }
}
