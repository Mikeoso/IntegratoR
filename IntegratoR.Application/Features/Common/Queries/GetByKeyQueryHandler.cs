using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.Application.Features.Common.Queries;

/// <summary>
/// Retrieves a single entity by its key via the <see cref="GetByKeyQuery{TEntity}"/>, supporting both simple and composite D365 F&amp;O keys.
/// </summary>
/// <typeparam name="TEntity">The type of the entity being queried.</typeparam>
public class GetByKeyQueryHandler<TEntity> : IRequestHandler<GetByKeyQuery<TEntity>, Result<TEntity>>
    where TEntity : class, IEntity
{
    private readonly ILogger<GetByKeyQueryHandler<TEntity>> _logger;
    private readonly IService<TEntity> _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetByKeyQueryHandler{TEntity}"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostics.</param>
    /// <param name="service">The service for the specified entity type.</param>
    public GetByKeyQueryHandler(ILogger<GetByKeyQueryHandler<TEntity>> logger, IService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    /// <summary>
    /// Asynchronously handles the <see cref="GetByKeyQuery{TEntity}"/> request.
    /// </summary>
    /// <param name="request">The query request, containing the key object for the lookup.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A successful <see cref="Result{T}"/> wrapping the found entity, or a failed result carrying the service errors.</returns>
    public async Task<Result<TEntity>> Handle(GetByKeyQuery<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetByKeyQuery for {Entity} with key values: {@CompositeKey}", typeof(TEntity).Name, request.CompositeKey);

        var entityResult = await _service.GetByKeyAsync(request.CompositeKey, cancellationToken).ConfigureAwait(false);

        return entityResult.Match(
            onSuccess: entity =>
            {
                _logger.LogDebug("Successfully retrieved {Entity} with key values: {@CompositeKey}", typeof(TEntity).Name, request.CompositeKey);
                return Result.Ok(entity);
            },
            onFailure: _ => Result.Fail<TEntity>(entityResult.Errors));
    }
}
