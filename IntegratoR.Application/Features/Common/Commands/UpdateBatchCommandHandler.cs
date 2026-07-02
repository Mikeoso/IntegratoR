using FluentResults;
using IntegratoR.Abstractions.Common.Batch;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.Application.Features.Common.Commands;

/// <summary>
/// Updates a batch of entities via the <see cref="UpdateBatchCommand{TEntity}"/>.
/// </summary>
/// <typeparam name="TEntity">The type of the entities being updated.</typeparam>
public class UpdateBatchCommandHandler<TEntity>
    : IRequestHandler<UpdateBatchCommand<TEntity>, Result<BatchOutcome>>
    where TEntity : class, IEntity
{
    private readonly ILogger<UpdateBatchCommandHandler<TEntity>> _logger;
    private readonly IBatchService<TEntity> _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateBatchCommandHandler{TEntity}"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostics.</param>
    /// <param name="service">The batch service for the specified entity type.</param>
    public UpdateBatchCommandHandler(ILogger<UpdateBatchCommandHandler<TEntity>> logger, IBatchService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    /// <summary>
    /// Asynchronously handles the <see cref="UpdateBatchCommand{TEntity}"/> request.
    /// </summary>
    /// <param name="request">The command request, containing the entities to update.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A successful <see cref="Result{BatchOutcome}"/>, or a failed result carrying the batch service errors.</returns>
    public async Task<Result<BatchOutcome>> Handle(UpdateBatchCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating {Count} {EntityType} entities in batch.", request.Entities.Count, typeof(TEntity).Name);

        return await _service.UpdateBatchAsync(request.Entities, request.Options, cancellationToken).ConfigureAwait(false);
    }
}
