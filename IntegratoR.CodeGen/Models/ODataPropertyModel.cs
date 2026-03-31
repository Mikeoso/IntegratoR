namespace IntegratoR.CodeGen.Models;

/// <summary>
/// Represents a parsed OData property from CSDL metadata,
/// including D365-specific annotations.
/// </summary>
public sealed record ODataPropertyModel
{
    /// <summary>OData property name (e.g., "dataAreaId").</summary>
    public required string Name { get; init; }

    /// <summary>EDM type (e.g., "Edm.String", "Edm.Int32", "Microsoft.Dynamics.DataEntities.NoYes").</summary>
    public required string EdmType { get; init; }

    /// <summary>Whether the property is nullable in the CSDL schema.</summary>
    public bool IsNullable { get; init; } = true;

    /// <summary>Whether the property is part of the entity key.</summary>
    public bool IsKey { get; init; }

    /// <summary>D365 annotation: AllowEdit. Defaults to true.</summary>
    public bool AllowEdit { get; init; } = true;

    /// <summary>D365 annotation: AllowEditOnCreate. Defaults to true.</summary>
    public bool AllowEditOnCreate { get; init; } = true;

    /// <summary>D365 annotation: IsRequired.</summary>
    public bool IsRequired { get; init; }

    /// <summary>D365 annotation: LabelId (e.g., "@SYS13342").</summary>
    public string? Label { get; init; }

    /// <summary>Whether the EDM type references an enum (non-Edm. prefix in the D365 namespace).</summary>
    public bool IsEnum { get; init; }

    /// <summary>The enum type name if <see cref="IsEnum"/> is true (e.g., "NoYes").</summary>
    public string? EnumTypeName { get; init; }
}
