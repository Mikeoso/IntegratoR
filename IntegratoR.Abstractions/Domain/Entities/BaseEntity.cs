using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Telemetry;
using System.Reflection;

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
public abstract class BaseEntity<TKey> : IEntity<TKey>, IContext
{
    /// <summary>
    /// Gets or sets the unique identifier for this entity.
    /// </summary>
    /// <remarks>
    /// This property is marked as <see langword="virtual"/> to allow derived classes to provide a custom
    /// implementation if needed. For instance, a derived entity might override this property to
    /// handle a composite key or a key that requires specific formatting before being set.
    /// </remarks>
    public required virtual object[] Id { get; set; }

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
    public virtual IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return this.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToDictionary(p => p.Name, p => p.GetValue(this) ?? new object());
    }
}