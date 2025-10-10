namespace IntegratoR.Abstractions.Interfaces.Entity;

/// <summary>
/// Defines the foundational contract for all domain entities, ensuring each has a strongly-typed primary key.
/// </summary>
/// <typeparam name="TKey">The data type of the entity's primary key.</typeparam>
/// <remarks>
/// This interface is a cornerstone of our architecture. By having our domain entities implement
/// <c>IEntity&lt;TKey&gt;</c>, we can create generic repositories, specifications, and CQRS handlers
/// (e.g., <c>GetByIdQuery&lt;TEntity, TKey&gt;</c>) that operate on any entity type.
/// This drastically reduces boilerplate code and enforces a consistent pattern for data access.
///
/// It provides a standardized way to access an entity's identifier, abstracting the specific
/// key field names used in the underlying D365 F&O data entities.
/// </remarks>
public interface IEntity
{
    /// <summary>
    /// Gets the composite primary key that uniquely identifies this entity.
    /// </summary>
    /// <returns>
    /// An array of objects representing the values of the key fields. The order of values in the array is crucial and must be consistent.
    /// </returns>
    /// <remarks>
    /// This method is essential for entities with composite keys. It abstracts the specific properties
    /// that constitute the key, enabling generic patterns (like the Repository or Specification pattern)
    /// to retrieve or process entities by their complete key.
    ///
    /// In D365 F&O, many entities feature composite keys, which often include a <c>DataAreaId</c> in combination
    /// with other fields (e.g., <c>SalesOrderNumber</c>, <c>JournalBatchNumber</c>).
    /// </remarks>
    object[] GetCompositeKey();

    /// <summary>
    /// Creates a read-only dictionary that captures the entity's state for logging purposes.
    /// </summary>
    /// <returns>
    /// An <see cref="IReadOnlyDictionary{TKey, TValue}"/> containing the public instance properties of the entity and their values.
    /// </returns>
    /// <remarks>
    /// This method uses reflection to iterate over all public, readable instance properties of the derived class.
    /// It is particularly useful for structured logging, where an object's state is captured as key-value pairs.
    /// Properties whose value is <see langword="null"/> are replaced with a new <see cref="object"/> to avoid null reference issues in logging contexts.
    /// Indexed properties are excluded from the output.
    /// </remarks>
    IReadOnlyDictionary<string, object> GetLoggingContext();
}