using FluentResults;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Abstractions.Common.CQRS.Commands
{
    /// <summary>
    /// Represents a generic command to create a new entity.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity to create.</typeparam>
    /// <param name="Entity">The entity to create.</param>
    public record CreateCommand<TEntity>(TEntity Entity) : ICommand<Result<TEntity>>
        where TEntity : IEntity
    {
        /// <summary>
        /// Gets the structured logging context for this command.
        /// </summary>
        /// <returns>The entity's own logging context, or a fallback containing the entity type name when <see cref="Entity"/> is <see langword="null"/>.</returns>
        public virtual IReadOnlyDictionary<string, object> GetLoggingContext()
        {
            // LoggingBehaviour runs before validation, so a null Entity must not throw here.
            return Entity?.GetLoggingContext()
                ?? new Dictionary<string, object> { { "EntityType", typeof(TEntity).Name } };
        }
    }
}
