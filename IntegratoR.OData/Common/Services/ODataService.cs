using IntegratoR.Abstractions.Common.Result;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.OData.Common.Annotations;
using IntegratoR.OData.Interfaces.Services;
using Simple.OData.Client;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;

namespace IntegratoR.OData.Common.Services;

/// <summary>
/// A generic service that provides a concrete implementation for data access operations
/// against a D365 F&O OData endpoint using Simple.OData.Client.
/// </summary>
/// <typeparam name="TEntity">The type of the entity, which must be a class implementing <see cref="IEntity{TKey}"/>.</typeparam>
/// <typeparam name="TKey">The type of the entity's primary key.</typeparam>
/// <remarks>
/// This class serves as the default repository for all entities in the system. It handles
/// CRUD operations, complex queries, and batch operations. It also encapsulates error handling,
/// catching <see cref="WebRequestException"/> from the OData client and converting them into
/// the application's standard <see cref="Result"/> pattern for consistent error propagation.
/// </remarks>
public class ODataService<TEntity, TKey> : IODataService<TEntity, TKey>, IODataBatchService<TEntity, TKey> where TEntity : class, IEntity<TKey>
{
    private readonly IODataClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataService{TEntity, TKey}"/> class.
    /// </summary>
    /// <param name="client">The configured <see cref="IODataClient"/> instance, provided by dependency injection.</param>
    public ODataService(IODataClient client)
    {
        _client = client;
    }

    #region IService Implementation

    /// <inheritdoc />
    /// <remarks>
    /// This method translates to an OData POST request. It uses the <see cref="CreatePayload"/>
    /// helper to build the request body, respecting metadata attributes like <see cref="ODataFieldAttribute"/>
    /// to exclude server-generated fields.
    /// </remarks>
    public async Task<Result<TEntity>> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = CreatePayload(entity, isCreateOperation: true);
            var addedEntity = await _client
                                    .For<TEntity>()
                                    .Set(payload)
                                    .InsertEntryAsync(true, cancellationToken);  // 'true' returns the created entity from the server

            return Result<TEntity>.Ok(addedEntity);
        }
        catch (WebRequestException ex)
        {
            return Result<TEntity>.Fail(new Error(
                $"{typeof(TEntity).Name}.AddFailed",
                $"Failed to add entity: {ex.Message}",
                ErrorType.Failure,
                ex));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The Simple.OData.Client library translates the provided LINQ expression tree directly
    /// into an OData `$filter` query string, enabling strongly-typed filtering.
    /// </remarks>
    public async Task<Result<IEnumerable<TEntity>>> FindAsync(Expression<Func<TEntity, bool>>? filter, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _client.For<TEntity>();
            if (filter is not null)
            {
                query = query.Filter(filter);
            }
            var entities = await query.FindEntriesAsync(cancellationToken);
            return Result<IEnumerable<TEntity>>.Ok(entities);
        }
        catch (WebRequestException ex)
        {
            return Result<IEnumerable<TEntity>>.Fail(new Error(
                $"{typeof(TEntity).Name}.QueryFailed",
                $"OData query failed: {ex.Message}",
                ErrorType.Failure,
                ex));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method is designed to handle both simple keys (e.g., a single string or int) and
    /// composite keys (using an anonymous object, like `new { Key1 = "A", Key2 = 1 }`).
    /// </remarks>
    public async Task<Result<TEntity>> GetByKeyAsync(object keyValues, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _client
                .For<TEntity>()
                .Key(keyValues)
                .FindEntryAsync(cancellationToken);

            if (entity is null)
            {
                return Result<TEntity>.Fail(new Error(
                    $"{typeof(TEntity).Name}.NotFound",
                    "Entity with the specified composite key was not found.",
                    ErrorType.NotFound));
            }

            return Result<TEntity>.Ok(entity);
        }
        catch (WebRequestException ex)
        {
            return Result<TEntity>.Fail(new Error(
                $"{typeof(TEntity).Name}.RequestFailed",
                $"OData request failed: {ex.Message}",
                ErrorType.Failure,
                ex));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method translates to an OData PATCH request. Simple.OData.Client intelligently
    /// sends only the modified properties of the entity.
    /// </remarks>
    public async Task<Result<TEntity>> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity == null)
        {
            return Result<TEntity>.Fail(new Error(
                "Validation.NullEntity", "The provided entity cannot be null.", ErrorType.Validation));
        }

        try
        {
            var updatedEntity = await _client
                                    .For<TEntity>()
                                    .Key(entity) // Extracts key from the entity itself
                                    .Set(entity) // Sets the payload
                                    .UpdateEntryAsync(cancellationToken);
            return Result<TEntity>.Ok(updatedEntity);
        }
        catch (WebRequestException ex)
        {
            return Result<TEntity>.Fail(new Error(
                $"{typeof(TEntity).Name}.UpdateFailed",
                $"Failed to update entity with ID '{entity.Id}': {ex.Message}",
                ErrorType.Failure,
                ex));
        }
    }

    /// <inheritdoc />
    /// <remarks>This method translates to an OData DELETE request.</remarks>
    public async Task<Result> DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        if (id == null)
        {
            return Result<TEntity>.Fail(new Error( // Returns a non-generic Result on failure
                "Validation.NullKey", "The provided ID cannot be null.", ErrorType.Validation));
        }

        try
        {
            await _client
                .For<TEntity>()
                .Key(id)
                .DeleteEntryAsync(cancellationToken);
            return Result.Ok();
        }
        catch (WebRequestException ex)
        {
            return Result.Fail(new Error(
                $"{typeof(TEntity).Name}.DeleteFailed",
                $"Failed to delete entity with ID '{id}': {ex.Message}",
                ErrorType.Failure,
                ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<TEntity>> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        if (id == null)
        {
            return Result<TEntity>.Fail(new Error(
                "Validation.NullKey", "The provided ID cannot be null.", ErrorType.Validation));
        }

        try
        {
            var entity = await _client
                .For<TEntity>()
                .Key(id)
                .FindEntryAsync(cancellationToken);

            if (entity is null)
            {
                return Result<TEntity>.Fail(new Error(
                    $"{typeof(TEntity).Name}.NotFound",
                    $"Entity with the specified key {id} was not found.",
                    ErrorType.NotFound));
            }

            return Result<TEntity>.Ok(entity);
        }
        catch (WebRequestException ex)
        {
            return Result<TEntity>.Fail(new Error(
                $"{typeof(TEntity).Name}.RequestFailed",
                $"OData request failed: {ex.Message}",
                ErrorType.Failure,
                ex));
        }
    }
    #endregion

    #region IODataService Implementation

    /// <inheritdoc />
    /// <remarks>
    /// This method provides a flexible way to build complex queries by mapping parameters
    /// directly to OData query options like `$filter`, `$expand`, `$select`, `$skip`, and `$top`.
    /// Note: The `orderBy` parameter is not implemented in this version.
    /// </remarks>
    public async Task<Result<IEnumerable<TEntity>>> QueryAsync(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, Expression<Func<TEntity, object>>? expand = null, Expression<Func<TEntity, object>>? select = null, int? skip = null, int? top = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _client.For<TEntity>();

            if (filter is not null) query = query.Filter(filter);
            if (expand is not null) query = query.Expand(expand);
            if (select is not null) query = query.Select(select);
            if (skip.HasValue) query = query.Skip(skip.Value);
            if (top.HasValue) query = query.Top(top.Value);

            var entities = await query.FindEntriesAsync(cancellationToken);
            return Result<IEnumerable<TEntity>>.Ok(entities);
        }
        catch (WebRequestException ex)
        {
            return Result<IEnumerable<TEntity>>.Fail(new Error(
                $"{typeof(TEntity).Name}.QueryFailed",
                $"OData query failed: {ex.Message}",
                ErrorType.Failure,
                ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<IEnumerable<TEntity>>> FindAll(CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _client
                            .For<TEntity>()
                            .FindEntriesAsync(cancellationToken);

            if (entity is null)
            {
                return Result<IEnumerable<TEntity>>.Fail(new Error(
                $"{typeof(TEntity).Name}.QueryFailed",
                $"Failed to query entity data set",
                ErrorType.Failure));
            }

            // FindEntriesAsync returns an empty collection, not null, if no entries are found.
            // A null result would indicate a more serious deserialization or transport issue,
            // which is more likely to throw a WebRequestException.

            return Result<IEnumerable<TEntity>>.Ok(entity);
        }
        catch (WebRequestException ex)
        {
            return Result<IEnumerable<TEntity>>.Fail(new Error(
                $"{typeof(TEntity).Name}.RequestFailed",
                $"OData request failed: {ex.Message}",
                ErrorType.Failure,
                ex));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method efficiently retrieves only the count from the server by translating
    /// the call to an OData `$count` segment query.
    /// </remarks>

    public async Task<Result<int>> CountAsync(Expression<Func<TEntity, bool>>? filter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _client.For<TEntity>();
            if (filter is not null)
            {
                query = query.Filter(filter);
            }
            var count = await query.Count().FindScalarAsync<int>(cancellationToken);
            return Result<int>.Ok(count);
        }
        catch (WebRequestException ex)
        {
            return Result<int>.Fail(new Error(
                $"{typeof(TEntity).Name}.CountFailed",
                $"OData count query failed: {ex.Message}",
                ErrorType.Failure,
                ex));
        }
    }
    #endregion

    #region IODataBatchService Implementation

    /// <inheritdoc />
    /// <remarks>
    /// This method groups multiple POST operations into a single OData `$batch` request,
    /// which significantly reduces network latency and provides "all-or-nothing" transactional behavior.
    /// </remarks>
    public async Task<Result> AddBatchAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        try
        {
            var batch = new ODataBatch(_client);
            foreach (var entity in entities)
            {
                batch += c => c.For<TEntity>().Set(entity).InsertEntryAsync(cancellationToken);
            }
            await batch.ExecuteAsync(cancellationToken);
            return Result.Ok();
        }
        catch (WebRequestException ex)
        {
            return Result.Fail(new Error(
               $"{typeof(TEntity).Name}.AddBatchFailed",
               $"OData batch add operation failed: {ex.Message}",
               ErrorType.Failure,
               ex));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method groups multiple DELETE operations into a single OData `$batch` request.
    /// </remarks>
    public async Task<Result> DeleteBatchAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        try
        {
            var batch = new ODataBatch(_client);
            foreach (var id in ids)
            {
                batch += c => c.For<TEntity>().Key(id!).DeleteEntryAsync(cancellationToken);
            }
            await batch.ExecuteAsync(cancellationToken);
            return Result.Ok();
        }
        catch (WebRequestException ex)
        {
            return Result.Fail(new Error(
               $"{typeof(TEntity).Name}.DeleteBatchFailed",
               $"OData batch delete operation failed: {ex.Message}",
               ErrorType.Failure,
               ex));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method groups multiple PATCH operations into a single OData `$batch` request.
    /// </remarks>
    public async Task<Result> UpdateBatchAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        try
        {
            var batch = new ODataBatch(_client);
            foreach (var entity in entities)
            {
                batch += c => c.For<TEntity>().Key(entity.Id!).Set(entity).UpdateEntryAsync(cancellationToken);
            }
            await batch.ExecuteAsync(cancellationToken);
            return Result.Ok();
        }
        catch (WebRequestException ex)
        {
            return Result.Fail(new Error(
               $"{typeof(TEntity).Name}.UpdateBatchFailed",
               $"OData batch update operation failed: {ex.Message}",
               ErrorType.Failure,
               ex));
        }
    }
    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Dynamically creates a payload dictionary from an entity instance. It respects various metadata
    /// attributes to include or exclude properties based on the operation type and serialization rules.
    /// </summary>
    /// <param name="entity">The entity instance to serialize.</param>
    /// <param name="isCreateOperation">A flag to indicate if the payload is for a create (true) or update (false) operation.</param>
    /// <returns>A dictionary of property names and values to be sent in the OData request body.</returns>
    /// <remarks>
    /// This method reflects over the entity's properties and applies the following rules:
    /// 1. Ignores properties that are not readable/writable, or are marked with <see cref="NotMappedAttribute"/> or <see cref="JsonIgnoreAttribute"/>.
    /// 2. Honors the <see cref="ODataFieldAttribute"/> to exclude properties specifically for create or update operations.
    /// 3. Skips properties that have not been set (i.e., still hold their default value) to create a cleaner payload.
    /// 4. Uses the name from <see cref="JsonPropertyNameAttribute"/> if present, otherwise defaults to the property name.
    /// </remarks>
    private Dictionary<string, object> CreatePayload(TEntity entity, bool isCreateOperation)
    {
        var payload = new Dictionary<string, object>();
        var properties = typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            // Respect NotMapped and JsonIgnore attributes
            if (!property.CanRead || !property.CanWrite) continue;
            if (property.GetCustomAttribute<NotMappedAttribute>() is not null) continue;
            if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null) continue;

            var attribute = property.GetCustomAttribute<ODataFieldAttribute>();

            // Check for custom odata annotation
            if (isCreateOperation && attribute?.IgnoreOnCreate == true) continue;
            if (!isCreateOperation && attribute?.IgnoreOnUpdate == true) continue;

            var value = property.GetValue(entity);

            // Only add fields to the collection that were set (i.e., not default values)
            var defaultValue = property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null;
            if (value is not null && !value.Equals(defaultValue))
            {
                var propertyName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
                payload.Add(propertyName, value);
            }
        }
        return payload;
    }
    #endregion
}
