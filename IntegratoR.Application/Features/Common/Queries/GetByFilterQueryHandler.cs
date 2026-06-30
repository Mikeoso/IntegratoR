using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.Application.Features.Common.Queries;

/// <summary>
/// A reusable, generic MediatR query handler responsible for processing the <see cref="GetByFilterQuery{TEntity}"/>.
/// It retrieves a collection of entities of a specified type that match a given filter expression.
/// </summary>
/// <typeparam name="TEntity">The type of the entity being queried. Must be a class that implements <see cref="IEntity"/>.</typeparam>
/// <remarks>
/// This class leverages C# generics to provide a single implementation for a common data retrieval scenario.
/// When a request like `GetByFilterQuery&lt;Customer&gt;` is dispatched via MediatR, the dependency injection
/// container will automatically construct an instance of `GetByFilterQueryHandler&lt;Customer&gt;`
/// and inject the corresponding `IService&lt;Customer&gt;`.
///
/// The handler's role is simply to delegate the data access to the injected service, which in turn
/// is responsible for translating the LINQ expression into the appropriate OData `$filter` query for D365 F&O.
/// </remarks>
public class GetByFilterQueryHandler<TEntity> : IRequestHandler<GetByFilterQuery<TEntity>, Result<IEnumerable<TEntity>>>
    where TEntity : class, IEntity
{
    private readonly IService<TEntity> _service;
    private readonly ILogger<GetByFilterQueryHandler<TEntity>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetByFilterQueryHandler{TEntity}"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostics.</param>
    /// <param name="service">The generic repository/service for the specified entity type.</param>
    public GetByFilterQueryHandler(ILogger<GetByFilterQueryHandler<TEntity>> logger, IService<TEntity> service)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Handles the incoming <see cref="GetByFilterQuery{TEntity}"/> request.
    /// </summary>
    /// <param name="request">The query request, containing the filter expression.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that represents the asynchronous operation.
    /// The task result contains a <see cref="Result{T}"/> wrapping the collection of found entities on success,
    /// or an error on failure.
    /// </returns>
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
