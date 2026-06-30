using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.Application.Features.Common.Commands;

/// <summary>
/// A generic handler that can process any command inheriting from <see cref="DeleteBatchCommand{TEntity}"/>.
/// </summary>
public class DeleteBatchCommandHandler<TEntity>
    : IRequestHandler<DeleteBatchCommand<TEntity>, Result>
    where TEntity : class, IEntity
{
    private readonly ILogger<DeleteBatchCommandHandler<TEntity>> _logger;
    private readonly IBatchService<TEntity> _service;

    public DeleteBatchCommandHandler(ILogger<DeleteBatchCommandHandler<TEntity>> logger, IBatchService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<Result> Handle(DeleteBatchCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting {Count} {EntityType} entities in batch.", request.Entities.Count, typeof(TEntity).Name);

        return await _service.DeleteBatchAsync(request.Entities, cancellationToken).ConfigureAwait(false);
    }
}
