using System.Collections.Concurrent;
using System.Reflection;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Telemetry;

namespace IntegratoR.Abstractions.Domain.Entities;

/// <summary>
/// Provides a foundational abstract base class for domain entities within the solution.
/// It establishes a common contract for entity identification via a composite key and for
/// structured-logging context capture.
/// </summary>
/// <remarks>
/// In a Domain-Driven Design (DDD) context, classes deriving from <c>BaseEntity</c> represent objects
/// defined not by their attributes, but by their thread of continuity and identity. This base class
/// helps decouple the core domain model from the data persistence layer,
/// promoting a cleaner and more maintainable architecture.
/// </remarks>
public abstract class BaseEntity : IEntity, IContext
{
    /// <summary>
    /// Caches, per concrete entity <see cref="Type"/>, the public readable non-indexed properties used
    /// by <see cref="GetLoggingContext"/>. Reflection over a type's property set is invariant, so the
    /// discovery is performed once per type and reused on every subsequent call.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> LoggingPropertyCache = new();

    /// <summary>
    /// Gets the composite primary key that uniquely identifies this entity.
    /// </summary>
    /// <returns>
    /// An array of objects representing the values of the key fields. The order of values in the array is crucial and must be consistent.
    /// </returns>
    /// <remarks>
    /// This method is essential for entities with composite keys. It abstracts the specific properties
    /// that constitute the key, enabling generic patterns to retrieve or process entities by their complete key.
    ///
    /// In D365 F&O, many entities feature composite keys, which often include a <c>DataAreaId</c> in combination
    /// with other fields (e.g., <c>SalesOrderNumber</c>, <c>JournalBatchNumber</c>).
    /// </remarks>
    public abstract object[] GetCompositeKey();

    /// <summary>
    /// Creates a read-only dictionary that captures the entity's state for logging purposes.
    /// </summary>
    /// <returns>
    /// An <see cref="IReadOnlyDictionary{TKey, TValue}"/> containing the public instance properties of the entity and their values.
    /// </returns>
    /// <remarks>
    /// This method uses reflection to iterate over all public, readable instance properties of the derived class.
    /// The per-type property set is cached so the reflection cost is paid once per concrete type.
    /// It is particularly useful for structured logging, where an object's state is captured as key-value pairs.
    /// Properties whose value is <see langword="null"/> are replaced with a new <see cref="object"/> to avoid null reference issues in logging contexts.
    /// Indexed properties are excluded from the output.
    /// </remarks>
    public virtual IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        PropertyInfo[] props = LoggingPropertyCache.GetOrAdd(
            GetType(),
            static t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .ToArray());

        return props.ToDictionary(p => p.Name, p => p.GetValue(this) ?? new object());
    }
}

/// <summary>
/// Provides a foundational abstract base class for domain entities, parameterised by a primary-key type.
/// </summary>
/// <typeparam name="TKey">The data type of the entity's primary key (e.g., <see cref="long"/>, <see cref="string"/>, <see cref="Guid"/>).</typeparam>
/// <remarks>
/// <para>
/// The <typeparamref name="TKey"/> type parameter is unused — the contract is expressed entirely through
/// <see cref="BaseEntity.GetCompositeKey"/>. Derive from the non-generic <see cref="BaseEntity"/> instead.
/// </para>
/// </remarks>
[Obsolete("since v1.4.0; the TKey type parameter is unused — derive from the non-generic BaseEntity instead; removed next MAJOR")]
public abstract class BaseEntity<TKey> : BaseEntity
{
}
