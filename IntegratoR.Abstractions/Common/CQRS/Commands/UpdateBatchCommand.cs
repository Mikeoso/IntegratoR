using System;
using System.Collections.Generic;
using System.Text;
using FluentResults;
using IntegratoR.Abstractions.Common.Batch;
using IntegratoR.Abstractions.Interfaces.Commands;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Abstractions.Common.CQRS.Commands
{
    /// <summary>
    /// Represents a generic command to update a batch of entities.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entities to update.</typeparam>
    /// <param name="Entities">The entities to update.</param>
    /// <param name="Options">Optional per-call batch failure-mode and chunk-size overrides.</param>
    public record UpdateBatchCommand<TEntity>(IReadOnlyList<TEntity> Entities, BatchOptions? Options = null) : ICommand<Result<BatchOutcome>>
        where TEntity : IEntity
    {
        /// <summary>
        /// Gets the structured logging context for this command.
        /// </summary>
        /// <returns>A context containing the batch entity count.</returns>
        public virtual IReadOnlyDictionary<string, object> GetLoggingContext()
        {
            // LoggingBehaviour runs before validation, so a null Entities collection must not throw here.
            return new Dictionary<string, object>
            {
                { "Count", Entities?.Count ?? 0 }
            };
        }
    }
}
