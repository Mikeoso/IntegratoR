using FluentResults;
using IntegratoR.Abstractions.Common.Batch;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.Application.Features.Common.Commands;

/// <summary>
/// Deletes a batch of entities via the <see cref="DeleteBatchCommand{TEntity}"/>.
/// </summary>
/// <typeparam name="TEntity">The type of the entities being deleted.</typeparam>
public class DeleteBatchCommandHandler<TEntity>
    : IRequestHandler<DeleteBatchCommand<TEntity>, Result<BatchOutcome>>
    where TEntity : class, IEntity
{
    private readonly ILogger<DeleteBatchCommandHandler<TEntity>> _logger;
    private readonly IBatchService<TEntity> _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteBatchCommandHandler{TEntity}"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostics.</param>
    /// <param name="service">The batch service for the specified entity type.</param>
    public DeleteBatchCommandHandler(ILogger<DeleteBatchCommandHandler<TEntity>> logger, IBatchService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    /// <summary>
    /// Asynchronously handles the <see cref="DeleteBatchCommand{TEntity}"/> request.
    /// </summary>
    /// <param name="request">The command request, containing the entities to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A successful <see cref="Result{BatchOutcome}"/>, or a failed result carrying the batch service errors.</returns>
    public async Task<Result<BatchOutcome>> Handle(DeleteBatchCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting {Count} {EntityType} entities in batch.", request.Entities.Count, typeof(TEntity).Name);

        return await _service.DeleteBatchAsync(request.Entities, request.Options, cancellationToken).ConfigureAwait(false);
    }
}
