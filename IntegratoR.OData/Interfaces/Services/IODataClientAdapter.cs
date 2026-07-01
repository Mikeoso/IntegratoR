using System.Linq.Expressions;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.OData.Domain.Models;

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
    /// <param name="entitySet">The OData entity set name.</param>
    /// <param name="key">
    /// Either a scalar key value (for single-key entities) or an
    /// <see cref="IDictionary{TKey, TValue}"/> of <c>wireName → value</c> pairs for composite
    /// keys. For composite keys, each dictionary key must be the OData wire name of the
    /// property — i.e. what would appear in a <c>$filter</c> expression — not the CLR property
    /// name. Framework callers obtain these names from entity reflection via
    /// <c>PropertyNameResolver</c> (which honours <c>[JsonPropertyName]</c>). Wire names must
    /// match <c>^[A-Za-z_][A-Za-z0-9_.]*$</c>; any other shape is rejected because the adapter
    /// interpolates the key verbatim into <c>$filter</c>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TEntity?> FindByKeyAsync<TEntity>(
        string entitySet,
        object key,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity;

    /// <summary>
    /// Queries entities with optional filter, expand, select, order-by, skip, and top parameters.
    /// </summary>
    /// <param name="entitySet">The OData entity set to query.</param>
    /// <param name="filter">An optional predicate applied as the OData <c>$filter</c> option.</param>
    /// <param name="expand">An optional navigation expression applied as the OData <c>$expand</c> option.</param>
    /// <param name="select">An optional projection expression applied as the OData <c>$select</c> option.</param>
    /// <param name="orderBy">
    /// Optional ordered list of <c>(keySelector, descending)</c> tuples translated into an OData
    /// <c>$orderby</c> clause. Each key selector honours <c>[JsonPropertyName]</c> on the member
    /// path, so D365 camelCase wire names (e.g. <c>dataAreaId</c>) sort correctly.
    /// </param>
    /// <param name="skip">The number of entities to skip, applied as the OData <c>$skip</c> option.</param>
    /// <param name="top">The maximum number of entities to return, applied as the OData <c>$top</c> option.</param>
    /// <param name="cancellationToken">A token that cancels the outbound OData request.</param>
    /// <returns>The entities matching the specified query options.</returns>
    Task<IEnumerable<TEntity>> FindEntriesAsync<TEntity>(
        string entitySet,
        Expression<Func<TEntity, bool>>? filter = null,
        Expression<Func<TEntity, object>>? expand = null,
        Expression<Func<TEntity, object>>? select = null,
        IReadOnlyList<(Expression<Func<TEntity, object>> KeySelector, bool Descending)>? orderBy = null,
        int? skip = null,
        int? top = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity;

    /// <summary>
    /// Updates an existing entity via OData PATCH.
    /// The payload can be a <typeparamref name="TEntity"/> instance or an <see cref="IDictionary{TKey, TValue}"/> for partial payloads.
    /// </summary>
    /// <param name="entitySet">The OData entity set name.</param>
    /// <param name="key">
    /// Either a scalar key value (for single-key entities) or an
    /// <see cref="IDictionary{TKey, TValue}"/> of <c>wireName → value</c> pairs for composite
    /// keys. Composite (dictionary) keys are routed through a raw-HttpClient bypass that PATCHes
    /// the keyed URL <c>EntitySet(field=literal,…)</c>, because PanoramicData's <c>Key(object)</c>
    /// path cannot bind a dictionary. Each dictionary key must be the OData wire name
    /// (what appears in a <c>$filter</c>), obtained from entity reflection via
    /// <c>PropertyNameResolver</c>, and must match <c>^[A-Za-z_][A-Za-z0-9_.]*$</c>.
    /// </param>
    /// <param name="payload">The PATCH payload (entity instance or partial dictionary).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TEntity> UpdateAsync<TEntity>(
        string entitySet,
        object key,
        object payload,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity;

    /// <summary>
    /// Deletes an entity by key via OData DELETE.
    /// </summary>
    /// <param name="entitySet">The OData entity set name.</param>
    /// <param name="key">
    /// Either a scalar key value (for single-key entities) or an
    /// <see cref="IDictionary{TKey, TValue}"/> of <c>wireName → value</c> pairs for composite
    /// keys. Composite (dictionary) keys are routed through a raw-HttpClient bypass that DELETEs
    /// the keyed URL <c>EntitySet(field=literal,…)</c>, because PanoramicData's <c>Key(object)</c>
    /// path cannot bind a dictionary. Each dictionary key must be the OData wire name
    /// (what appears in a <c>$filter</c>), obtained from entity reflection via
    /// <c>PropertyNameResolver</c>, and must match <c>^[A-Za-z_][A-Za-z0-9_.]*$</c>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
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
    /// Creates multiple entities in an atomic batch changeset using filtered payloads.
    /// Returns per-operation results for diagnostics.
    /// </summary>
    Task<IReadOnlyList<BatchOperationResult>> BatchCreateAsync(
        string entitySet,
        IEnumerable<IDictionary<string, object>> payloads,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates multiple entities in an atomic batch changeset using filtered payloads.
    /// Returns per-operation results for diagnostics.
    /// </summary>
    Task<IReadOnlyList<BatchOperationResult>> BatchUpdateAsync(
        string entitySet,
        IEnumerable<(object Key, IDictionary<string, object> Payload)> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes multiple entities by key in an atomic batch changeset.
    /// Returns per-operation results for diagnostics.
    /// </summary>
    Task<IReadOnlyList<BatchOperationResult>> BatchDeleteAsync(
        string entitySet,
        IEnumerable<object> keys,
        CancellationToken cancellationToken = default);
}
