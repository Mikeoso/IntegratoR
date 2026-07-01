using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;

namespace IntegratoR.OData.Interfaces.Services;

/// <summary>
/// Defines the OData-specific contract for bundling create, update, and delete operations on
/// multiple entities into a single <c>$batch</c> request.
/// </summary>
/// <typeparam name="TEntity">The type of the entity for the batch operations.</typeparam>
/// <remarks>
/// The batch members are declared on the layer-agnostic <see cref="IBatchService{TEntity}"/> so the
/// generic batch command handlers can depend on the abstraction without referencing the OData layer;
/// this interface is the OData-specific marker those implementations register against. Batch members
/// are executed within a single transaction, giving an all-or-nothing guarantee.
/// </remarks>
public interface IODataBatchService<TEntity> : IBatchService<TEntity> where TEntity : IEntity
{
}
