using System.Text.Json;
using IntegratoR.Abstractions.Common.Results.SystemText;
using IntegratoR.Abstractions.Interfaces.Services;
using Microsoft.Extensions.Caching.Distributed;

namespace IntegratoR.Application.Common.Services;

/// <summary>
/// A distributed implementation of the <see cref="ICacheService"/> interface,
/// utilizing the standard <see cref="IDistributedCache"/> abstraction.
/// </summary>
/// <remarks>
/// This service is intended for scaled-out, multi-instance environments (e.g., Azure Functions
/// on a Consumption or Premium plan) where cache consistency across instances is required.
/// The underlying provider is configured via DI — typically backed by Azure Cache for Redis
/// using <c>Microsoft.Extensions.Caching.StackExchangeRedis</c>.
///
/// Consumers choose between <see cref="InMemoryCacheService"/> and <see cref="DistributedCacheService"/>
/// by registering the appropriate implementation against <see cref="ICacheService"/> in the DI container.
/// </remarks>
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
    /// <param name="cache">The <see cref="IDistributedCache"/> instance provided by the DI container.</param>
    public DistributedCacheService(IDistributedCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc />
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
    public async Task RemoveAsync(string cacheKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);

        await _cache.RemoveAsync(cacheKey).ConfigureAwait(false);
    }
}
