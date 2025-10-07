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
public interface IEntity<TKey>
{
    /// <summary>
    /// Gets or sets the unique identifier for this entity.
    /// </summary>
    /// <remarks>
    /// For D365 F&O entities, this typically maps to the entity's primary key, such as a
    /// string-based natural key (e.g., SalesOrderNumber) or a long for RecId-based keys.
    /// </remarks>
    TKey Id { get; set; }
}