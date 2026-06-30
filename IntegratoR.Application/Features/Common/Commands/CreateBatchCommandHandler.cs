using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.Application.Features.Common.Commands;

/// <summary>
/// A generic handler that can process any command inheriting from <see cref="CreateBatchCommand{TEntity}"/>.
/// </summary>
public class CreateBatchCommandHandler<TEntity>
    : IRequestHandler<CreateBatchCommand<TEntity>, Result>
    where TEntity : class, IEntity
{
    private readonly ILogger<CreateBatchCommandHandler<TEntity>> _logger;
    private readonly IBatchService<TEntity> _service;

    public CreateBatchCommandHandler(ILogger<CreateBatchCommandHandler<TEntity>> logger, IBatchService<TEntity> service)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<Result> Handle(CreateBatchCommand<TEntity> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating {Count} {EntityType} entities in batch.", request.Entities.Count, typeof(TEntity).Name);

        return await _service.AddBatchAsync(request.Entities, cancellationToken).ConfigureAwait(false);
    }
}
