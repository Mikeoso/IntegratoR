using System.Threading.Tasks;
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;

namespace IntegratoR.Application.Features.Common.Commands
{
    /// <summary>
    /// Creates a single entity via the <see cref="CreateCommand{TEntity}"/>.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity being created.</typeparam>
    public class CreateCommandHandler<TEntity>
        : IRequestHandler<CreateCommand<TEntity>, Result<TEntity>>
        where TEntity : class, IEntity
    {
        private readonly IService<TEntity> _service;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateCommandHandler{TEntity}"/> class.
        /// </summary>
        /// <param name="service">The service for the specified entity type.</param>
        public CreateCommandHandler(IService<TEntity> service)
        {
            _service = service;
        }

        /// <summary>
        /// Asynchronously handles the <see cref="CreateCommand{TEntity}"/> request.
        /// </summary>
        /// <param name="request">The command request, containing the entity to create.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A successful <see cref="Result{T}"/> wrapping the created entity, or a failed result carrying the service errors.</returns>
        public async Task<Result<TEntity>> Handle(CreateCommand<TEntity> request, CancellationToken cancellationToken)
        {
            return await _service.AddAsync(request.Entity, cancellationToken).ConfigureAwait(false);
        }
    }
}
