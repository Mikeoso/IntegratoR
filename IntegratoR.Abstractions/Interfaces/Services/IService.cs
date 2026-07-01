using System.Linq.Expressions;
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Abstractions.Interfaces.Services;

/// <summary>
/// Defines the generic data-access abstraction for an entity type, exposing CRUD and query operations.
/// </summary>
/// <typeparam name="TEntity">The type of the entity, which must implement <see cref="IEntity"/>.</typeparam>
/// <remarks>This interface is itself the data-access abstraction; do not wrap it in an additional repository layer.</remarks>
public interface IService<TEntity> where TEntity : IEntity
{
    /// <summary>
    /// Asynchronously retrieves a single entity by its simple or composite key.
    /// </summary>
    /// <param name="keyValues">The ordered key field values that identify the entity.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result{TEntity}"/> containing the entity if found, or a <c>NotFound</c> error.</returns>
    Task<Result<TEntity>> GetByKeyAsync(object[] keyValues, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously finds the entities that match a specified filter expression.
    /// </summary>
    /// <param name="filter">A LINQ expression used to filter the entities; if <see langword="null"/>, all entities are returned.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result{TValue}"/> containing the matching entities on success, or an error on failure.</returns>
    Task<Result<IEnumerable<TEntity>>> FindAsync(Expression<Func<TEntity, bool>>? filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously adds a new entity to the data source.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result{TEntity}"/> containing the created entity, including any server-generated values.</returns>
    Task<Result<TEntity>> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously updates an existing entity in the data source.
    /// </summary>
    /// <param name="entity">The entity with its updated values.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result{TEntity}"/> containing the entity state after the update.</returns>
    Task<Result<TEntity>> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deletes an entity from the data source by its key.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A non-generic <see cref="Result"/> indicating the success or failure of the deletion.</returns>
    Task<Result> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
}
