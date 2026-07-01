using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.Application.Features.Common.Queries;

/// <summary>
/// Retrieves the entities matching a filter expression via the <see cref="GetByFilterQuery{TEntity}"/>.
/// </summary>
/// <typeparam name="TEntity">The type of the entity being queried.</typeparam>
public class GetByFilterQueryHandler<TEntity> : IRequestHandler<GetByFilterQuery<TEntity>, Result<IEnumerable<TEntity>>>
    where TEntity : class, IEntity
{
    private readonly IService<TEntity> _service;
    private readonly ILogger<GetByFilterQueryHandler<TEntity>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetByFilterQueryHandler{TEntity}"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostics.</param>
    /// <param name="service">The service for the specified entity type.</param>
    public GetByFilterQueryHandler(ILogger<GetByFilterQueryHandler<TEntity>> logger, IService<TEntity> service)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Asynchronously handles the <see cref="GetByFilterQuery{TEntity}"/> request.
    /// </summary>
    /// <param name="request">The query request, containing the filter expression.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A successful <see cref="Result{T}"/> wrapping the matching entities, or a failed result carrying the service errors.</returns>
    public async Task<Result<IEnumerable<TEntity>>> Handle(GetByFilterQuery<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetByFilterQuery for {EntityType} with filter: {Filter}", typeof(TEntity).Name, request.Filter.ToString());

        var entitiesResult = await _service.FindAsync(request.Filter, cancellationToken).ConfigureAwait(false);

        return entitiesResult.Match(
            onSuccess: entities =>
            {
                var result = entities as ICollection<TEntity> ?? entities.ToList();
                _logger.LogDebug("Retrieved {Count} entities of type {EntityType}", result.Count, typeof(TEntity).Name);

                return Result.Ok<IEnumerable<TEntity>>(result);
            },
            onFailure: _ =>
            {
                return Result.Fail<IEnumerable<TEntity>>(entitiesResult.Errors);
            });
    }
}
