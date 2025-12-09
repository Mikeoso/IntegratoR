using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace IntegratoR.Application.Features.Common.Commands
{
    public class DeleteCommandHandler<TEntity> : IRequestHandler<DeleteCommand<TEntity>, Result>
        where TEntity : class, IEntity
    {
        private readonly IService<TEntity> _service;

        public DeleteCommandHandler(IService<TEntity> service)
        {
            _service = service;
        }

        public async Task<Result> Handle(DeleteCommand<TEntity> request, CancellationToken cancellationToken)
        {
            return await _service.DeleteAsync(request.Entity, cancellationToken);
        }
    }
}
