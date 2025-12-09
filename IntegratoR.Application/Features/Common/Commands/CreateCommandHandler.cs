using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;
using MediatR;
using System.Threading.Tasks;

namespace IntegratoR.Application.Features.Common.Commands
{
    /// <summary>
    /// A generic handler that can process any command inheriting from CreateCommand.
    /// </summary>
    public class CreateCommandHandler<TEntity>
        : IRequestHandler<CreateCommand<TEntity>, Result<TEntity>>
        where TEntity : class, IEntity
    {
        private readonly IService<TEntity> _service;

        public CreateCommandHandler(IService<TEntity> service)
        {
            _service = service;
        }

        public async Task<Result<TEntity>> Handle(CreateCommand<TEntity> request, CancellationToken cancellationToken)
        {
            return await _service.AddAsync(request.Entity, cancellationToken);
        }
    }
}