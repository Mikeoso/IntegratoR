using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.Application.Features.Common.Commands;

/// <summary>
/// Creates a batch of entities via the <see cref="CreateBatchCommand{TEntity}"/>.
/// </summary>
/// <typeparam name="TEntity">The type of the entities being created.</typeparam>
public class CreateBatchCommandHandler<TEntity>
    : IRequestHandler<CreateBatchCommand<TEntity>, Result>
    where TEntity : class, IEntity
{
    private readonly ILogger<CreateBatchCommandHandler<TEntity>> _logger;
    private readonly IBatchService<TEntity> _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateBatchCommandHandler{TEntity}"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostics.</param>
    /// <param name="service">The batch service for the specified entity type.</param>
    public CreateBatchCommandHandler(ILogger<CreateBatchCommandHandler<TEntity>> logger, IBatchService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    /// <summary>
    /// Asynchronously handles the <see cref="CreateBatchCommand{TEntity}"/> request.
    /// </summary>
    /// <param name="request">The command request, containing the entities to create.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A successful <see cref="Result"/>, or a failed result carrying the batch service errors.</returns>
    public async Task<Result> Handle(CreateBatchCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating {Count} {EntityType} entities in batch.", request.Entities.Count, typeof(TEntity).Name);

        return await _service.AddBatchAsync(request.Entities, cancellationToken).ConfigureAwait(false);
    }
}
