using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Entity;
using System.Linq.Expressions;

namespace IntegratoR.Abstractions.Interfaces.Services;

/// <summary>
/// Defines a generic repository pattern for data access, abstracting CRUD (Create, Read, Update, Delete)
/// and query operations for a specific entity type.
/// </summary>
/// <typeparam name="TEntity">The type of the entity, which must implement <see cref="IEntity{TKey}"/>.</typeparam>
/// <typeparam name="TKey">The type of the entity's primary key.</typeparam>
/// <remarks>
/// While named <c>IService</c>, this interface's role is that of a **Repository**. It isolates the application
/// and domain layers from the data source's implementation details.
///
/// A concrete implementation (e.g., <c>ODataService&lt;TEntity, TKey&gt;</c>) would be responsible for
/// translating these method calls into specific OData HTTP requests against a OData endpoint,
/// typically using a library.
/// </remarks>
public interface IService<TEntity> where TEntity : IEntity
{
    /// <summary>
    /// Asynchronously retrieves a single entity by its key, supporting simple or composite keys.
    /// </summary>
    /// <param name="keyValues">An object representing the key. For composite keys, use an anonymous object (e.g., new { Key1 = "A", Key2 = 1 }).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result{TEntity}"/> containing the entity if found, or a 'NotFound' error.</returns>
    /// <remarks>This method translates to an OData GET request, handling composite keys like `.../data/SalesOrderLines(SalesOrderNumber='S01', LineNum=1.0m)`.</remarks>
    Task<Result<TEntity>> GetByKeyAsync(object[] keyValues, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously finds a collection of entities that match a specified filter expression.
    /// </summary>
    /// <param name="filter">A LINQ expression tree to filter the entities. If null, all entities are returned.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result{TValue}"/> containing the collection of found entities.</returns>
    /// <remarks>The implementation will convert the LINQ expression into an OData `$filter` query string, enabling powerful, type-safe querying of OData Endpoint.</remarks>
    Task<Result<IEnumerable<TEntity>>> FindAsync(Expression<Func<TEntity, bool>>? filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously adds a new entity to the data source.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result{TEntity}"/> containing the created entity as returned by the data source, including any server-generated values.</returns>
    /// <remarks>This method translates to an OData POST request.</remarks>
    Task<Result<TEntity>> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously updates an existing entity in the data source.
    /// </summary>
    /// <param name="entity">The entity with its updated values.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result{TEntity}"/> containing the state of the entity after the update.</returns>
    /// <remarks>This method translates to an OData PATCH request, only sending the modified properties to D365 F&O.</remarks>
    Task<Result<TEntity>> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deletes an entity from the data source using its primary key.
    /// </summary>
    /// <param name="id">The primary key of the entity to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A non-generic <see cref="Result"/> indicating the success or failure of the deletion.</returns>
    /// <remarks>This method translates to an OData DELETE request.</remarks>
    Task<Result> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
}