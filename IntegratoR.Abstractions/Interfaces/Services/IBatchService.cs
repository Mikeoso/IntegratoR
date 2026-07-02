using FluentResults;
using IntegratoR.Abstractions.Common.Batch;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Abstractions.Interfaces.Services;

/// <summary>
/// Defines create, update, and delete operations over multiple entities, submitted as OData
/// <c>$batch</c> requests and chunked automatically.
/// </summary>
/// <typeparam name="TEntity">The type of the entity for the batch operations.</typeparam>
/// <remarks>
/// The failure behaviour is configurable per call (<see cref="BatchOptions"/>) or via
/// <c>ODataSettings.Batch</c>: <see cref="BatchFailureMode.Atomic"/> runs each chunk as an all-or-nothing
/// changeset, while <see cref="BatchFailureMode.ContinueOnError"/> applies operations independently. The
/// returned <see cref="BatchOutcome"/> reports the per-item result; on failure it is carried by a
/// <c>BatchIntegrationError</c> so the failure list is retrievable from the failed result.
/// </remarks>
public interface IBatchService<TEntity> where TEntity : IEntity
{
    /// <summary>
    /// Creates a collection of entities, chunked into one or more <c>$batch</c> requests.
    /// </summary>
    /// <param name="entities">The entity instances to create.</param>
    /// <param name="options">Optional per-call overrides for failure mode and chunk size; <see langword="null"/> uses the configured defaults.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="BatchOutcome"/> on full success; a failed result carrying the outcome otherwise.</returns>
    Task<Result<BatchOutcome>> AddBatchAsync(IEnumerable<TEntity> entities, BatchOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a collection of entities, chunked into one or more <c>$batch</c> requests. Each entity must have its key populated.
    /// </summary>
    /// <param name="entities">The entity instances to update.</param>
    /// <param name="options">Optional per-call overrides for failure mode and chunk size; <see langword="null"/> uses the configured defaults.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="BatchOutcome"/> on full success; a failed result carrying the outcome otherwise.</returns>
    Task<Result<BatchOutcome>> UpdateBatchAsync(IEnumerable<TEntity> entities, BatchOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a collection of entities, chunked into one or more <c>$batch</c> requests. Each entity must have its key populated.
    /// </summary>
    /// <param name="entities">The entity instances to delete.</param>
    /// <param name="options">Optional per-call overrides for failure mode and chunk size; <see langword="null"/> uses the configured defaults.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="BatchOutcome"/> on full success; a failed result carrying the outcome otherwise.</returns>
    Task<Result<BatchOutcome>> DeleteBatchAsync(IEnumerable<TEntity> entities, BatchOptions? options = null, CancellationToken cancellationToken = default);
}
