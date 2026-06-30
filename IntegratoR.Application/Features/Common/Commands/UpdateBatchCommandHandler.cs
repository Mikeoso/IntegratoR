using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.Application.Features.Common.Commands;

/// <summary>
/// A generic handler that can process any command inheriting from <see cref="UpdateBatchCommand{TEntity}"/>.
/// </summary>
public class UpdateBatchCommandHandler<TEntity>
    : IRequestHandler<UpdateBatchCommand<TEntity>, Result>
    where TEntity : class, IEntity
{
    private readonly ILogger<UpdateBatchCommandHandler<TEntity>> _logger;
    private readonly IBatchService<TEntity> _service;

    public UpdateBatchCommandHandler(ILogger<UpdateBatchCommandHandler<TEntity>> logger, IBatchService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<Result> Handle(UpdateBatchCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating {Count} {EntityType} entities in batch.", request.Entities.Count, typeof(TEntity).Name);

        return await _service.UpdateBatchAsync(request.Entities, cancellationToken).ConfigureAwait(false);
    }
}
