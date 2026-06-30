using IntegratoR.Abstractions.Domain.Entities;

namespace IntegratoR.TestKit.Doubles.Entities;

/// <summary>
/// A single integer-key test entity for use in generic handler tests that require a simple key.
/// </summary>
public class TestSingleKeyEntity : BaseEntity
{
    /// <summary>
    /// Gets or sets the primary integer identifier.
    /// </summary>
    public required int Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the entity.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Returns the composite key formed by <see cref="Id"/>.
    /// </summary>
    public override object[] GetCompositeKey() => [Id];
}
