using FluentResults;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Abstractions.Interfaces.Services;

/// <summary>
/// Defines a contract for performing create, update, and delete operations on multiple entities in a single batch request.
/// </summary>
/// <typeparam name="TEntity">The type of the entity for the batch operations.</typeparam>
/// <remarks>Operations are bundled into a single round-trip and executed atomically, providing an all-or-nothing consistency guarantee.</remarks>
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
