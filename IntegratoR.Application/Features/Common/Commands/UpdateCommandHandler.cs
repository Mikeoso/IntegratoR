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
    public class UpdateCommandHandler<TEntity> : IRequestHandler<UpdateCommand<TEntity>, Result>
        where TEntity : class, IEntity
    {
        private readonly IService<TEntity> _service;

        public UpdateCommandHandler(IService<TEntity> service) 
        { 
            _service = service; 
        }

        public async Task<Result> Handle(UpdateCommand<TEntity> request, CancellationToken cancellationToken)
        {
            return await _service.UpdateAsync(request.Entity, cancellationToken);
        }
    }
}
