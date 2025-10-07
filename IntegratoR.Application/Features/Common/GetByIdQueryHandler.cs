using IntegratoR.Abstractions.Common.CQRS;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.Application.Features.Common;

/// <summary>
/// A reusable, generic MediatR query handler for processing the <see cref="GetByIdQuery{TEntity, TKey}"/>.
/// It retrieves a single entity of a specified type using its unique primary key.
/// </summary>
/// <typeparam name="TEntity">The type of the entity being queried. Must be a class that implements <see cref="IEntity{TKey}"/>.</typeparam>
/// <typeparam name="TKey">The type of the primary key for the entity.</typeparam>
/// <remarks>
/// This class provides a "one-size-fits-all" implementation for fetching entities by their ID.
/// When a request like `GetByIdQuery&lt;Customer, string&gt;` is dispatched, the dependency
/// injection container constructs an instance of `GetByIdQueryHandler&lt;Customer, string&gt;`
/// and injects the corresponding `IService&lt;Customer, string&gt;`.
///
/// The handler then delegates the data access call to the injected service, which is responsible
/// for translating the request into a specific OData key-based lookup for D365 F&O (e.g., GET /data/Customers('ID')).
/// </remarks>
public class GetByIdQueryHandler<TEntity, TKey> : IRequestHandler<GetByIdQuery<TEntity, TKey>, Result<TEntity>>
    where TEntity : class, IEntity<TKey>
{
    private readonly ILogger<GetByIdQueryHandler<TEntity, TKey>> _logger;
    private readonly IService<TEntity, TKey> _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetByIdQueryHandler{TEntity, TKey}"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostics.</param>
    /// <param name="service">The generic repository/service for the specified entity type.</param>
    public GetByIdQueryHandler(ILogger<GetByIdQueryHandler<TEntity, TKey>> logger, IService<TEntity, TKey> service)
    {
        _logger = logger;
        _service = service;
    }

    /// <summary>
    /// Handles the incoming <see cref="GetByIdQuery{TEntity, TKey}"/> request.
    /// </summary>
    /// <param name="request">The query request, containing the ID of the entity to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that represents the asynchronous operation.
    /// The task result contains a <see cref="Result{T}"/> wrapping the found entity on success,
    /// or a "NotFound" error on failure.
    /// </returns>
    public async Task<Result<TEntity>> Handle(GetByIdQuery<TEntity, TKey> request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetByIdQuery for entity {EntityType} with ID: {Id}", typeof(TEntity).Name, request.Id);

        var result = await _service.GetByIdAsync(request.Id, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to retrieve entity {EntityType} with ID: {Id}. Error: {ErrorCode} - {ErrorMessage}",
                typeof(TEntity).Name, request.Id, result.Error?.Code, result.Error?.Message);

            // Propagate the failure result from the service layer.
            return Result<TEntity>.Fail(result.Error!);
        }

        _logger.LogDebug("Successfully retrieved entity {EntityType} with ID: {Id}", typeof(TEntity).Name, request.Id);
        return Result<TEntity>.Ok(result.Value!);
    }
}