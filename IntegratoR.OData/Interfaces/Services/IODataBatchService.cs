using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Entity;

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
/// entities in a single batch request, leveraging the OData `$batch` capability.
/// </summary>
/// <typeparam name="TEntity">The type of the entity for the batch operations.</typeparam>
/// <typeparam name="TKey">The type of the entity's primary key.</typeparam>
/// <remarks>
/// Using batch operations is critical for performance in high-volume integrations. It allows
/// multiple individual operations to be bundled into a single network round-trip to the
/// D365 F&O server. These operations are typically executed within a single transaction,
/// providing an "all-or-nothing" guarantee for data consistency.
/// </remarks>
public interface IODataBatchService<TEntity> where TEntity : IEntity
{
    /// <summary>
    /// Adds a collection of entities in a single atomic batch operation.
    /// </summary>
    /// <param name="entities">The collection of entity instances to create.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A non-generic <see cref="Result"/> indicating the overall success or failure of the batch operation.</returns>
    /// <remarks>
    /// This method bundles multiple OData POST requests into a single `$batch` request.
    /// </remarks>
    Task<Result> AddBatchAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a collection of entities in a single atomic batch operation.
    /// </summary>
    /// <param name="entities">The collection of entity instances to update. Each entity must have its key populated.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A non-generic <see cref="Result"/> indicating the overall success or failure of the batch operation.</returns>
    /// <remarks>
    /// This method bundles multiple OData PATCH requests into a single `$batch` request.
    /// </remarks>
    Task<Result> UpdateBatchAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a collection of entities by their unique identifiers in a single atomic batch operation.
    /// </summary>
    /// <param name="ids">The collection of primary keys of the entities to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A non-generic <see cref="Result"/> indicating the overall success or failure of the batch operation.</returns>
    /// <remarks>
    /// This method bundles multiple OData DELETE requests into a single `$batch` request.
    /// </remarks>
    Task<Result> DeleteBatchAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
}