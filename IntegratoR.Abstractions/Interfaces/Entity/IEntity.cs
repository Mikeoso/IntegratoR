namespace IntegratoR.Abstractions.Interfaces.Entity;

/// <summary>
/// Defines the foundational contract for all domain entities, exposing a composite key and structured logging context.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Gets the composite key that uniquely identifies this entity.
    /// </summary>
    /// <returns>An ordered array of the key field values; the order must be consistent for the entity type.</returns>
    /// <remarks>D365 F&amp;O entities typically combine <c>DataAreaId</c> with a business key such as <c>SalesOrderNumber</c> or <c>JournalBatchNumber</c>.</remarks>
    object[] GetCompositeKey();

    /// <summary>
    /// Gets a read-only dictionary capturing the entity's state for structured logging.
    /// </summary>
    /// <returns>A dictionary of the entity's public instance property names and values.</returns>
    IReadOnlyDictionary<string, object> GetLoggingContext();
}
