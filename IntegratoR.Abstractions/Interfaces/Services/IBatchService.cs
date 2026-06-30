using FluentResults;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Abstractions.Interfaces.Services;

/// <summary>
/// Defines a contract for performing CUD (Create, Update, Delete) operations on multiple
/// entities in a single batch request.
/// </summary>
/// <typeparam name="TEntity">The type of the entity for the batch operations.</typeparam>
/// <remarks>
/// Batch operations are critical for performance in high-volume integrations: multiple
/// individual operations are bundled into a single network round-trip and typically executed
/// within a single transaction, providing an "all-or-nothing" consistency guarantee.
///
/// This interface lives in <c>IntegratoR.Abstractions</c> so the generic batch command handlers
/// in <c>IntegratoR.Application</c> can depend on it without referencing the OData layer.
/// </remarks>
public interface IBatchService<TEntity> where TEntity : IEntity
{
    /// <summary>
    /// Adds a collection of entities in a single atomic batch operation.
    /// </summary>
    /// <param name="entities">The collection of entity instances to create.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A non-generic <see cref="Result"/> indicating the overall success or failure of the batch operation.</returns>
    Task<Result> AddBatchAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a collection of entities in a single atomic batch operation.
    /// </summary>
    /// <param name="entities">The collection of entity instances to update. Each entity must have its key populated.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A non-generic <see cref="Result"/> indicating the overall success or failure of the batch operation.</returns>
    Task<Result> UpdateBatchAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a collection of entities in a single atomic batch operation.
    /// </summary>
    /// <param name="entities">The collection of entity instances to delete. Each entity must have its key populated.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A non-generic <see cref="Result"/> indicating the overall success or failure of the batch operation.</returns>
    Task<Result> DeleteBatchAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
}
