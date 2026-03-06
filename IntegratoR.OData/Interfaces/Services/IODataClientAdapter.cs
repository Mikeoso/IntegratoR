using System.Linq.Expressions;
using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.OData.Interfaces.Services;

/// <summary>
/// Abstracts the OData client operations required by <see cref="Common.Services.ODataService{TEntity}"/>.
/// Provides a mockable interface over PanoramicData.OData.Client's concrete <c>ODataClient</c> class.
/// </summary>
public interface IODataClientAdapter
{
    /// <summary>
    /// Creates a new entity via OData POST, returning the server response entity.
    /// The payload can be a <typeparamref name="TEntity"/> instance or an <see cref="IDictionary{TKey, TValue}"/> for partial payloads.
    /// </summary>
    Task<TEntity> CreateAsync<TEntity>(
        string entitySet,
        object payload,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity;

    /// <summary>
    /// Retrieves a single entity by its key. Returns null if not found.
    /// </summary>
    Task<TEntity?> FindByKeyAsync<TEntity>(
        string entitySet,
        object key,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity;

    /// <summary>
    /// Queries entities with optional filter, expand, select, skip, and top parameters.
    /// </summary>
    Task<IEnumerable<TEntity>> FindEntriesAsync<TEntity>(
        string entitySet,
        Expression<Func<TEntity, bool>>? filter = null,
        Expression<Func<TEntity, object>>? expand = null,
        Expression<Func<TEntity, object>>? select = null,
        int? skip = null,
        int? top = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity;

    /// <summary>
    /// Updates an existing entity via OData PATCH.
    /// </summary>
    Task<TEntity> UpdateAsync<TEntity>(
        string entitySet,
        object key,
        TEntity entity,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity;

    /// <summary>
    /// Deletes an entity by key via OData DELETE.
    /// </summary>
    Task DeleteAsync(
        string entitySet,
        object key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of entities matching an optional filter.
    /// </summary>
    Task<int> CountAsync<TEntity>(
        string entitySet,
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity;

    /// <summary>
    /// Creates multiple entities in an atomic batch changeset.
    /// </summary>
    Task BatchCreateAsync<TEntity>(
        string entitySet,
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity;

    /// <summary>
    /// Updates multiple entities in an atomic batch changeset.
    /// </summary>
    Task BatchUpdateAsync<TEntity>(
        string entitySet,
        IEnumerable<(object Key, TEntity Entity)> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity;

    /// <summary>
    /// Deletes multiple entities by key in an atomic batch changeset.
    /// </summary>
    Task BatchDeleteAsync(
        string entitySet,
        IEnumerable<object> keys,
        CancellationToken cancellationToken = default);
}
