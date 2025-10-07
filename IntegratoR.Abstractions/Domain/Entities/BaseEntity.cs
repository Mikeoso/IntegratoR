using IntegratoR.Abstractions.Interfaces.Entity;

namespace IntegratoR.Abstractions.Domain.Entities;

/// <summary>
/// Provides a foundational abstract base class for domain entities within the solution.
/// It establishes a common contract for entity identification by providing a generic primary key.
/// </summary>
/// <typeparam name="TKey">The data type of the entity's primary key (e.g., <see cref="long"/>, <see cref="string"/>, <see cref="Guid"/>).</typeparam>
/// <remarks>
/// In a Domain-Driven Design (DDD) context, classes deriving from <c>BaseEntity</c> represent objects
/// defined not by their attributes, but by their thread of continuity and identity. This base class
/// helps decouple the core domain model from the data persistence layer,
/// promoting a cleaner and more maintainable architecture.
/// </remarks>
public abstract class BaseEntity<TKey> : IEntity<TKey>
{
    /// <summary>
    /// Gets or sets the unique identifier for this entity.
    /// </summary>
    /// <remarks>
    /// This property is marked as <see langword="virtual"/> to allow derived classes to provide a custom
    /// implementation if needed. For instance, a derived entity might override this property to
    /// handle a composite key or a key that requires specific formatting before being set.
    /// </remarks>
    public virtual TKey Id { get; set; }
}