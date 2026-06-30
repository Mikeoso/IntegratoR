using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file defines a contract for performing high-performance, bulk data modifications.
// It abstracts the concept of OData batch processing, which is essential for integrations
// that need to create, update, or delete a large number of records efficiently.
// </remarks>
// ---------------------------------------------------------------------------------------------

namespace IntegratoR.OData.Interfaces.Services;

/// <summary>
/// Defines a contract for performing CUD (Create, Update, Delete) operations on multiple
/// entities in a single batch request, leveraging the OData <c>$batch</c> capability.
/// </summary>
/// <typeparam name="TEntity">The type of the entity for the batch operations.</typeparam>
/// <remarks>
/// Using batch operations is critical for performance in high-volume integrations. It allows
/// multiple individual operations to be bundled into a single network round-trip to the
/// D365 F&amp;O server. These operations are typically executed within a single transaction,
/// providing an "all-or-nothing" guarantee for data consistency.
///
/// The batch members themselves are declared on the layer-agnostic
/// <see cref="IBatchService{TEntity}"/> (in <c>IntegratoR.Abstractions</c>) so the generic
/// batch command handlers can depend on the abstraction without referencing the OData layer.
/// This interface is the OData-specific marker that <see cref="IBatchService{TEntity}"/>
/// implementations register against.
/// </remarks>
public interface IODataBatchService<TEntity> : IBatchService<TEntity> where TEntity : IEntity
{
}
