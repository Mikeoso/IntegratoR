using System;
using System.Collections.Generic;
using System.Text;
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;

namespace IntegratoR.Application.Features.Common.Commands
{
    /// <summary>
    /// Updates a single entity via the <see cref="UpdateCommand{TEntity}"/>.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity being updated.</typeparam>
    public class UpdateCommandHandler<TEntity> : IRequestHandler<UpdateCommand<TEntity>, Result<TEntity>>
        where TEntity : class, IEntity
    {
        private readonly IService<TEntity> _service;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateCommandHandler{TEntity}"/> class.
        /// </summary>
        /// <param name="service">The service for the specified entity type.</param>
        public UpdateCommandHandler(IService<TEntity> service)
        {
            _service = service;
        }

        /// <summary>
        /// Asynchronously handles the <see cref="UpdateCommand{TEntity}"/> request.
        /// </summary>
        /// <param name="request">The command request, containing the entity to update.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A successful <see cref="Result{T}"/> wrapping the updated entity, or a failed result carrying the service errors.</returns>
        public async Task<Result<TEntity>> Handle(UpdateCommand<TEntity> request, CancellationToken cancellationToken)
        {
            return await _service.UpdateAsync(request.Entity, cancellationToken).ConfigureAwait(false);
        }
    }
}
