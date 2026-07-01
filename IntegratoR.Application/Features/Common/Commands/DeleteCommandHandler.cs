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
    /// Deletes a single entity via the <see cref="DeleteCommand{TEntity}"/>.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity being deleted.</typeparam>
    public class DeleteCommandHandler<TEntity> : IRequestHandler<DeleteCommand<TEntity>, Result<TEntity>>
        where TEntity : class, IEntity
    {
        private readonly IService<TEntity> _service;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteCommandHandler{TEntity}"/> class.
        /// </summary>
        /// <param name="service">The service for the specified entity type.</param>
        public DeleteCommandHandler(IService<TEntity> service)
        {
            _service = service;
        }

        /// <summary>
        /// Asynchronously handles the <see cref="DeleteCommand{TEntity}"/> request.
        /// </summary>
        /// <param name="request">The command request, containing the entity to delete.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A successful <see cref="Result{T}"/> wrapping the deleted entity, or a failed result carrying the service errors.</returns>
        /// <remarks>Remaps the non-generic delete <see cref="Result"/> to <c>Result.Ok(request.Entity)</c> on success.</remarks>
        public async Task<Result<TEntity>> Handle(DeleteCommand<TEntity> request, CancellationToken cancellationToken)
        {
            var result = await _service.DeleteAsync(request.Entity, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess
                ? Result.Ok(request.Entity)
                : Result.Fail<TEntity>(result.Errors);
        }
    }
}
