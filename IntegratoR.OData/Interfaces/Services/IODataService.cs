using System;
using System.Linq.Expressions;
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Services;

namespace IntegratoR.OData.Interfaces.Services;

/// <summary>
/// Extends the generic <see cref="IService{TEntity}"/> with features specific to the OData protocol,
/// such as expanding related entities, selecting specific fields, ordering, and paging.
/// </summary>
/// <typeparam name="TEntity">The type of the entity being queried.</typeparam>
public interface IODataService<TEntity> : IService<TEntity> where TEntity : IEntity
{
    /// <summary>
    /// Asynchronously retrieves a collection of entities using a comprehensive set of OData query options.
    /// </summary>
    /// <param name="filter">A LINQ expression to filter the entities. Corresponds to the OData `$filter` query option.</param>
    /// <param name="orderBy">A function to specify the ordering of the entities. Corresponds to the OData `$orderby` query option.</param>
    /// <param name="expand">A LINQ expression to include related entities (navigation properties). Corresponds to the OData `$expand` query option.</param>
    /// <param name="select">A LINQ expression to select a subset of properties. Corresponds to the OData `$select` query option.</param>
    /// <param name="skip">The number of entities to skip for paging. Corresponds to the OData `$skip` query option.</param>
    /// <param name="top">The maximum number of entities to return. Corresponds to the OData `$top` query option.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result{T}"/> containing a collection of entities that match the specified criteria.</returns>
    [Obsolete("Since v1.4.0; the Func-based orderBy was never applied to the OData query (it was silently dropped). Use the overload taking IReadOnlyList<(Expression<Func<TEntity, object>> KeySelector, bool Descending)> orderBy.")]
    Task<Result<IEnumerable<TEntity>>> QueryAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Expression<Func<TEntity, object>>? expand = null,
        Expression<Func<TEntity, object>>? select = null,
        int? skip = null,
        int? top = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a collection of entities using a comprehensive set of OData query
    /// options, including a strongly-typed <c>$orderby</c> specification that honours
    /// <c>[JsonPropertyName]</c> on each key selector.
    /// </summary>
    /// <param name="filter">A LINQ expression to filter the entities. Corresponds to the OData `$filter` query option.</param>
    /// <param name="orderBy">
    /// An ordered list of <c>(keySelector, descending)</c> tuples specifying the sort order.
    /// Corresponds to the OData `$orderby` query option. Each key selector's member path honours
    /// <c>[JsonPropertyName]</c>, so D365 camelCase wire names sort correctly.
    /// </param>
    /// <param name="expand">A LINQ expression to include related entities (navigation properties). Corresponds to the OData `$expand` query option.</param>
    /// <param name="select">A LINQ expression to select a subset of properties. Corresponds to the OData `$select` query option.</param>
    /// <param name="skip">The number of entities to skip for paging. Corresponds to the OData `$skip` query option.</param>
    /// <param name="top">The maximum number of entities to return. Corresponds to the OData `$top` query option.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result{T}"/> containing a collection of entities that match the specified criteria.</returns>
    Task<Result<IEnumerable<TEntity>>> QueryAsync(
        Expression<Func<TEntity, bool>>? filter,
        IReadOnlyList<(Expression<Func<TEntity, object>> KeySelector, bool Descending)>? orderBy,
        Expression<Func<TEntity, object>>? expand = null,
        Expression<Func<TEntity, object>>? select = null,
        int? skip = null,
        int? top = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves all entities of the specified type from the data set.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result{T}"/> containing the complete collection of entities.</returns>
    /// <remarks>
    /// <b>Caution:</b> Use this method judiciously on data entities that may contain a large
    /// number of records, as it can result in significant network payload and impact performance.
    /// Consider using filtering or paging (`QueryAsync`) where possible.
    /// </remarks>
    Task<Result<IEnumerable<TEntity>>> FindAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves all entities of the specified type from the data set.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result{T}"/> containing the complete collection of entities.</returns>
    /// <remarks>
    /// <b>Caution:</b> Use this method judiciously on data entities that may contain a large
    /// number of records, as it can result in significant network payload and impact performance.
    /// Consider using filtering or paging (`QueryAsync`) where possible.
    /// </remarks>
    [Obsolete("since v1.4.0; use FindAllAsync; removed next MAJOR")]
    Task<Result<IEnumerable<TEntity>>> FindAll(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets the total count of entities that match an optional filter.
    /// </summary>
    /// <param name="filter">An optional LINQ expression to filter the entities to be counted.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result{T}"/> containing the total count of matching entities.</returns>
    /// <remarks>
    /// This method translates to an OData `$count` query, which is highly efficient as it performs
    /// the count on the server and returns only a single integer value.
    /// </remarks>
    Task<Result<int>> CountAsync(Expression<Func<TEntity, bool>>? filter = null, CancellationToken cancellationToken = default);
}
