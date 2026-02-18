using IntegratoR.Abstractions.Common.CQRS.Commands;
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace IntegratoR.Application.Features.Common.Commands
{
    public class DeleteCommandHandler<TEntity> : IRequestHandler<DeleteCommand<TEntity>, Result<TEntity>>
        where TEntity : class, IEntity
    {
        private readonly IService<TEntity> _service;

        public DeleteCommandHandler(IService<TEntity> service)
        {
            _service = service;
        }

        public async Task<Result<TEntity>> Handle(DeleteCommand<TEntity> request, CancellationToken cancellationToken)
        {
            var result = await _service.DeleteAsync(request.Entity, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess
                ? Result.Ok(request.Entity)
                : Result.Fail<TEntity>(result.Errors);
        }
    }
}
