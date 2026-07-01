using System.Collections.Concurrent;
using System.Reflection;
using IntegratoR.Abstractions.Interfaces.Entity;
using IntegratoR.Abstractions.Interfaces.Telemetry;

namespace IntegratoR.Abstractions.Domain.Entities;

/// <summary>
/// Provides a base class for domain entities, establishing a common contract for identification via a
/// composite key and for structured-logging context capture.
/// </summary>
public abstract class BaseEntity : IEntity, IContext
{
    // Caches the public readable non-indexed properties per concrete entity type for GetLoggingContext.
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> LoggingPropertyCache = new();

    /// <summary>
    /// Gets the composite primary key that uniquely identifies this entity.
    /// </summary>
    /// <returns>
    /// An array of key-field values, in a consistent order that generic patterns rely on.
    /// </returns>
    /// <remarks>
    /// D365 F&amp;O entities typically key on <c>DataAreaId</c> combined with a business field such as
    /// <c>SalesOrderNumber</c> or <c>JournalBatchNumber</c>.
    /// </remarks>
    public abstract object[] GetCompositeKey();

    /// <summary>
    /// Gets a read-only dictionary that captures the entity's public property values for structured logging.
    /// </summary>
    /// <returns>
    /// An <see cref="IReadOnlyDictionary{TKey, TValue}"/> keyed by property name. Indexed properties are
    /// excluded, and <see langword="null"/> values are replaced with a new <see cref="object"/> to avoid
    /// null issues in logging contexts.
    /// </returns>
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
/// Provides a base class for domain entities, parameterised by a primary-key type.
/// </summary>
/// <typeparam name="TKey">The type of the entity's primary key.</typeparam>
/// <remarks>
/// The <typeparamref name="TKey"/> type parameter is unused — the contract is expressed entirely through
/// <see cref="BaseEntity.GetCompositeKey"/>. Derive from the non-generic <see cref="BaseEntity"/> instead.
/// </remarks>
[Obsolete("since v1.4.0; the TKey type parameter is unused — derive from the non-generic BaseEntity instead; removed next MAJOR")]
public abstract class BaseEntity<TKey> : BaseEntity
{
}
