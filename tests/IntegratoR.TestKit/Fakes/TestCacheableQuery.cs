using IntegratoR.Abstractions.Interfaces.Queries;

namespace IntegratoR.TestKit.Fakes;

/// <summary>
/// A configurable implementation of <see cref="ICacheableQuery{TResponse}"/> for use in
/// <c>CachingBehaviour</c> tests.
/// Allows tests to control the cache key, duration, and key values without creating real query types.
/// </summary>
/// <typeparam name="TResponse">The response type of the cacheable query.</typeparam>
public sealed class TestCacheableQuery<TResponse> : ICacheableQuery<TResponse>
{
    private readonly string _cacheKey;
    private readonly TimeSpan? _cacheDuration;
    private readonly object[] _cacheKeyValues;

    /// <summary>
    /// Initialises a new instance of <see cref="TestCacheableQuery{TResponse}"/>.
    /// </summary>
    /// <param name="cacheKey">The static cache key for this query.</param>
    /// <param name="cacheDuration">The cache duration, or <c>null</c> to bypass caching.</param>
    /// <param name="cacheKeyValues">The values used to generate the cache key.</param>
    public TestCacheableQuery(string cacheKey, TimeSpan? cacheDuration = null, object[]? cacheKeyValues = null)
    {
        _cacheKey = cacheKey;
        _cacheDuration = cacheDuration;
        _cacheKeyValues = cacheKeyValues ?? [];
    }

    /// <inheritdoc/>
    public string CacheKey => _cacheKey;

    /// <inheritdoc/>
    public TimeSpan? CacheDuration => _cacheDuration;

    // GetCacheKeyValues/GenerateCacheKey are [Obsolete] on ICacheableQuery but must still be
    // implemented until the next MAJOR removes the interface members.
#pragma warning disable CS0618 // Type or member is obsolete
    /// <inheritdoc/>
    public object[] GetCacheKeyValues() => _cacheKeyValues;

    /// <inheritdoc/>
    public string GenerateCacheKey() => _cacheKey;
#pragma warning restore CS0618 // Type or member is obsolete

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> GetLoggingContext()
        => new Dictionary<string, object>
        {
            { nameof(CacheKey), CacheKey },
            { nameof(CacheDuration), CacheDuration?.ToString() ?? "null" }
        };
}
