using IntegratoR.Abstractions.Interfaces.Queries;
using IntegratoR.Abstractions.Interfaces.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.Application.Common.Behaviours;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file defines a MediatR pipeline behavior, which is a powerful mechanism for implementing
// cross-cutting concerns in a CQRS architecture. This behavior applies a caching strategy
// transparently, without requiring any changes to the core query handlers themselves.
// </remarks>
// ---------------------------------------------------------------------------------------------

/// <summary>
/// A MediatR pipeline behavior that transparently adds a caching layer to the query pipeline
/// for any request that implements the <see cref="ICacheableQuery{TResponse}"/> interface.
/// </summary>
/// <typeparam name="TRequest">The type of the MediatR request being handled.</typeparam>
/// <typeparam name="TResponse">The type of the response from the request handler, constrained to be an <see cref="IResult"/>.</typeparam>
/// <remarks>
/// This behavior is registered in the dependency injection container and automatically wraps around
/// the handlers for all MediatR requests. It demonstrates the power of the decorator pattern
/// for applying application-wide logic like caching, logging, or validation.
///
/// By handling caching here, the query handlers remain clean and focused on their single
/// responsibility: fetching data. They are completely unaware of the caching logic.
/// A key feature of this implementation is that it **only caches successful responses**,
/// preventing transient errors or "not found" results from being stored and served.
/// </remarks>
public class CachingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachingBehaviour<TRequest, TResponse>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingBehaviour{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="cacheService">The application's abstracted cache service (e.g., a Redis or in-memory implementation).</param>
    /// <param name="logger">The logger instance for diagnostics.</param>
    public CachingBehaviour(ICacheService cacheService, ILogger<CachingBehaviour<TRequest, TResponse>> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <summary>
    /// Intercepts an incoming MediatR request and applies caching logic before forwarding
    /// the request to the next behavior or the final handler.
    /// </summary>
    /// <param name="request">The incoming MediatR request object.</param>
    /// <param name="next">
    /// A delegate representing the next action in the pipeline. Calling this delegate will
    /// execute the next behavior or, ultimately, the request's handler.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The response from either the cache or the request handler.</returns>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // This behavior only acts on queries that explicitly opt into caching.
        // For all other requests, it's a simple passthrough.
        if (request is not ICacheableQuery<TResponse> cacheableQuery)
        {
            return await next();
        }

        // Attempt to retrieve the response from the cache using the key defined in the query.
        var cachedResponse = await _cacheService.GetAsync<TResponse>(cacheableQuery.CacheKey);
        if (cachedResponse is not null)
        {
            _logger.LogDebug("Cache HIT for key {CacheKey}. Returning cached response.", cacheableQuery.CacheKey);
            return cachedResponse; // Short-circuit the pipeline and return the cached value.
        }

        // If the item was not in the cache, proceed with executing the actual request handler.
        _logger.LogDebug("Cache MISS for key {CacheKey}. Executing handler.", cacheableQuery.CacheKey);
        var response = await next();

        // Only cache the response if the handler executed successfully.
        // This prevents caching failures or "Not Found" results.
        if (response is { IsSuccess: true })
        {
            _logger.LogDebug("Handler executed successfully. Caching response with key {CacheKey} for {CacheDuration}", cacheableQuery.CacheKey, cacheableQuery.CacheDuration);
            await _cacheService.SetAsync(cacheableQuery.CacheKey, response, cacheableQuery.CacheDuration);
        }

        return response;
    }
}