namespace IntegratoR.CodeGen.Models;

/// <summary>
/// Represents a fully parsed CSDL schema containing entity types, enum types,
/// and entity set mappings extracted from a D365 F&amp;O $metadata document.
/// </summary>
public sealed record CsdlSchema
{
    /// <summary>The schema namespace (e.g., "Microsoft.Dynamics.DataEntities").</summary>
    public required string Namespace { get; init; }

    /// <summary>All parsed entity types.</summary>
    public required IReadOnlyList<ODataEntityModel> EntityTypes { get; init; }

    /// <summary>All parsed enum types.</summary>
    public required IReadOnlyList<ODataEnumModel> EnumTypes { get; init; }

    /// <summary>Mapping from entity set name to entity type name.</summary>
    public required IReadOnlyDictionary<string, string> EntitySetMapping { get; init; }
}
