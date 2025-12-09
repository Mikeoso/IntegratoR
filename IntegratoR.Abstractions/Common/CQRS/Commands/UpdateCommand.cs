using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;
using System;
using System.Collections.Generic;
using System.Text;

namespace IntegratoR.Abstractions.Common.CQRS.Commands
{
    /// <summary>
    /// A generic base command for updating a entity.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity to update.</typeparam>
    public record UpdateCommand<TEntity>(TEntity Entity) : ICommand<Result<TEntity>>
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
