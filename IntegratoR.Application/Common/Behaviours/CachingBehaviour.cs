using FluentResults;
using IntegratoR.Abstractions.Interfaces.Queries;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.Application.Common.Behaviours;

/// <summary>
/// Provides a MediatR pipeline behaviour that adds a caching layer for any request implementing <see cref="ICacheableQuery{TResponse}"/>.
/// </summary>
/// <typeparam name="TRequest">The type of the MediatR request being handled.</typeparam>
/// <typeparam name="TResponse">The type of the response from the request handler.</typeparam>
/// <remarks>Only successful responses are cached, so transient failures and "not found" results are never served from the cache.</remarks>
public class CachingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResultBase
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachingBehaviour<TRequest, TResponse>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingBehaviour{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="cacheService">The cache service used to store and retrieve query responses.</param>
    /// <param name="logger">The logger instance for diagnostics.</param>
    public CachingBehaviour(ICacheService cacheService, ILogger<CachingBehaviour<TRequest, TResponse>> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <summary>
    /// Asynchronously applies caching around the request before forwarding it to the next pipeline step.
    /// </summary>
    /// <param name="request">The incoming MediatR request.</param>
    /// <param name="next">A delegate that invokes the next behaviour or the request handler.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The response served from the cache on a hit; otherwise the response from the handler.</returns>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // This behavior only acts on queries that explicitly opt into caching.
        // For all other requests, it's a simple passthrough.
        if (request is not ICacheableQuery<TResponse> cacheableQuery)
        {
            return await next().ConfigureAwait(false);
        }

        // Attempt to retrieve the response from the cache using the key defined in the query.
        var cachedResponse = await _cacheService.GetAsync<TResponse>(cacheableQuery.CacheKey).ConfigureAwait(false);
        if (cachedResponse is not null)
        {
            _logger.LogDebug("Cache HIT for key {CacheKey}. Returning cached response.", cacheableQuery.CacheKey);
            return cachedResponse; // Short-circuit the pipeline and return the cached value.
        }

        // If the item was not in the cache, proceed with executing the actual request handler.
        _logger.LogDebug("Cache MISS for key {CacheKey}. Executing handler.", cacheableQuery.CacheKey);
        var response = await next().ConfigureAwait(false);

        // Only cache the response if the handler executed successfully.
        // This prevents caching failures or "Not Found" results.
        if (response is { IsSuccess: true })
        {
            _logger.LogDebug("Handler executed successfully. Caching response with key {CacheKey} for {CacheDuration}", cacheableQuery.CacheKey, cacheableQuery.CacheDuration);
            await _cacheService.SetAsync(cacheableQuery.CacheKey, response, cacheableQuery.CacheDuration).ConfigureAwait(false);
        }

        return response;
    }
}
