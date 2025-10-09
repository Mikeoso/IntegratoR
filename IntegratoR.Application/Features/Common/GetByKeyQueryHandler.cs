using IntegratoR.Abstractions.Common.CQRS;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.Application.Features.Common;

/// <summary>
/// A reusable, generic MediatR query handler for processing the <see cref="GetByKeyQuery{TEntity, TKey}"/>.
/// It retrieves a single entity using a key object, making it suitable for entities with both
/// simple and composite primary keys.
/// </summary>
/// <typeparam name="TEntity">The type of the entity being queried. Must be a class that implements <see cref="IEntity{TKey}"/>.</typeparam>
/// <typeparam name="TKey">The type of the primary key for the entity.</typeparam>
/// <remarks>
/// This handler is essential for working with OData endpoints are identified by more
/// than one field. For example, to fetch a specific sales order line, you need both the sales order
/// number and the line number.
///
/// The handler delegates the data access call to the injected <see cref="IService{TEntity, TKey}"/>,
/// which is responsible for translating the provided key object into a valid composite key for an
/// OData URL (e.g., `.../data/SalesOrderLines(SalesOrderNumber='SO-123',LineNumber=1.0m)`).
/// </remarks>
public class GetByKeyQueryHandler<TEntity, TKey> : IRequestHandler<GetByKeyQuery<TEntity, TKey>, Result<TEntity>>
    where TEntity : class, IEntity<TKey>
{
    private readonly ILogger<GetByKeyQueryHandler<TEntity, TKey>> _logger;
    private readonly IService<TEntity, TKey> _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetByKeyQueryHandler{TEntity, TKey}"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostics.</param>
    /// <param name="service">The generic repository/service for the specified entity type.</param>
    public GetByKeyQueryHandler(ILogger<GetByKeyQueryHandler<TEntity, TKey>> logger, IService<TEntity, TKey> service)
    {
        _logger = logger;
        _service = service;
    }

    /// <summary>
    /// Handles the incoming <see cref="GetByKeyQuery{TEntity, TKey}"/> request.
    /// </summary>
    /// <param name="request">The query request, containing the key object for the lookup.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that represents the asynchronous operation.
    /// The task result contains a <see cref="Result{T}"/> wrapping the found entity on success,
    /// or a "NotFound" error on failure.
    /// </returns>
    public async Task<Result<TEntity>> Handle(GetByKeyQuery<TEntity, TKey> request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetByKeyQuery for {Entity} with key values: {@KeyValues}", typeof(TEntity).Name, request.keyValues);

        var entityResult = await _service.GetByKeyAsync(request.keyValues, cancellationToken);

        return entityResult.Match(
            onSuccess: entity =>
            {
                _logger.LogDebug("Successfully retrieved {Entity} with key values: {@KeyValues}", typeof(TEntity).Name, request.keyValues);
                return Result<TEntity>.Ok(entity);
            },
            onFailure: _ => Result<TEntity>.Fail(entityResult));
    }
}