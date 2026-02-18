using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.Common.Annotations;

namespace IntegratoR.TestKit.Doubles.Entities;

/// <summary>
/// A test entity decorated with <see cref="ODataFieldAttribute"/> to verify that OData payload
/// builders correctly exclude server-generated and read-only fields during create and update operations.
/// </summary>
public class TestEntityWithODataAttributes : BaseEntity<string>
{
    /// <summary>
    /// Gets or sets the primary identifier. Ignored on create because it is server-generated.
    /// </summary>
    [ODataField(IgnoreOnCreate = true)]
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the display name. Included in both create and update payloads.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets a read-only field. Ignored on update because it cannot be changed after creation.
    /// </summary>
    [ODataField(IgnoreOnUpdate = true)]
    public required string ReadOnlyField { get; set; }

    /// <summary>
    /// Gets or sets a mutable optional field. Included in both create and update payloads.
    /// </summary>
    public string? Mutable { get; set; }

    /// <summary>
    /// Returns the composite key formed by <see cref="Id"/>.
    /// </summary>
    public override object[] GetCompositeKey() => [Id];
}
