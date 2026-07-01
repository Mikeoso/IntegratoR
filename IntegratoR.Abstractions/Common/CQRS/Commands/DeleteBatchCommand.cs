using System;
using System.Collections.Generic;
using System.Text;
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Abstractions.Common.CQRS.Commands
{
    /// <summary>
    /// A generic base command for deleting entities in a batch.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity to create.</typeparam>
    public record DeleteBatchCommand<TEntity>(IReadOnlyList<TEntity> Entities) : ICommand<Result>
        where TEntity : IEntity
    {
        /// <summary>
        /// Provides a default logging context containing the entity information.
        /// This can be overridden in specific command implementations for more detail.
        /// </summary>
        public virtual IReadOnlyDictionary<string, object> GetLoggingContext()
        {
            // Null-safe: a null Entities collection is an invalid command that ValidationBehaviour
            // will reject; LoggingBehaviour runs first, so this must not throw.
            return new Dictionary<string, object>
            {
                { "Count", Entities?.Count ?? 0 }
            };
        }
    }
}
