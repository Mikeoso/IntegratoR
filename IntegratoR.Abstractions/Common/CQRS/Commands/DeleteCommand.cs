using System;
using System.Collections.Generic;
using System.Text;
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Abstractions.Common.CQRS.Commands
{
    /// <summary>
    /// A generic base command for deleting a entity.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity to delete.</typeparam>
    public record DeleteCommand<TEntity>(TEntity Entity) : ICommand<Result<TEntity>>
        where TEntity : IEntity
    {
        /// <summary>
        /// Provides a default logging context containing the entity type.
        /// This can be overridden in specific command implementations for more detail.
        /// </summary>
        public virtual IReadOnlyDictionary<string, object> GetLoggingContext()
        {
            // Null-safe: the record permits a null Entity (an invalid command that ValidationBehaviour
            // will reject). LoggingBehaviour runs before validation, so building the logging context
            // must not throw. Mirrors GetByKeyQuery.GetLoggingContext's null handling.
            return Entity?.GetLoggingContext()
                ?? new Dictionary<string, object> { { "EntityType", typeof(TEntity).Name } };
        }
    }
}
