using IntegratoR.Abstractions.Domain.Entities;

namespace IntegratoR.TestKit.Doubles.Entities;

/// <summary>
/// A composite-key test entity that mirrors the D365 F&O pattern of combining two string fields
/// as the primary key. Use in generic handler tests instead of production entities.
/// </summary>
public class TestEntity : BaseEntity<string>
{
    /// <summary>
    /// Gets or sets the primary identifier. First component of the composite key.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the partition key. Second component of the composite key.
    /// </summary>
    public required string PartitionKey { get; set; }

    /// <summary>
    /// Gets or sets the display name of the entity.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets an optional description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Returns the composite key formed by <see cref="Id"/> and <see cref="PartitionKey"/>.
    /// </summary>
    public override object[] GetCompositeKey() => [Id, PartitionKey];
}
