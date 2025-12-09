using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Abstractions.Common.CQRS.Commands
{
    /// <summary>
    /// A generic base command for creating a new entity.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity to create.</typeparam>
    public record CreateCommand<TEntity>(TEntity Entity) : ICommand<Result<TEntity>>
        where TEntity : IEntity
    {
        /// <summary>
        /// Provides a default logging context containing the entity type.
        /// This can be overridden in specific command implementations for more detail.
        /// </summary>
        public virtual IReadOnlyDictionary<string, object> GetLoggingContext()
        {
            return Entity.GetLoggingContext();
        }
    }
}
