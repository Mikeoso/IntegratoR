namespace IntegratoR.CodeGen.Models;

/// <summary>
/// Represents a parsed OData entity type from CSDL metadata.
/// </summary>
public sealed record ODataEntityModel
{
    /// <summary>The entity type name (e.g., "LedgerJournalHeader").</summary>
    public required string Name { get; init; }

    /// <summary>The entity set name from the EntityContainer (e.g., "LedgerJournalHeaders").</summary>
    public string? EntitySetName { get; init; }

    /// <summary>D365 entity-level annotation: IsReadOnly.</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>D365 annotation: LabelId for the entity type.</summary>
    public string? Label { get; init; }

    /// <summary>The properties of this entity type.</summary>
    public required IReadOnlyList<ODataPropertyModel> Properties { get; init; }

    /// <summary>The key property names.</summary>
    public required IReadOnlyList<string> KeyPropertyNames { get; init; }
}
