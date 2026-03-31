namespace IntegratoR.CodeGen.Models;

/// <summary>
/// Represents a parsed OData enum type from CSDL metadata.
/// </summary>
public sealed record ODataEnumModel
{
    /// <summary>The enum type name (e.g., "NoYes").</summary>
    public required string Name { get; init; }

    /// <summary>The enum members with their integer values.</summary>
    public required IReadOnlyList<ODataEnumMember> Members { get; init; }

    /// <summary>D365 annotation: LabelId for the enum type.</summary>
    public string? Label { get; init; }
}

/// <summary>
/// Represents a single member of an OData enum type.
/// </summary>
public sealed record ODataEnumMember
{
    /// <summary>The member name (e.g., "Yes").</summary>
    public required string Name { get; init; }

    /// <summary>The integer value (e.g., 1).</summary>
    public required int Value { get; init; }

    /// <summary>D365 annotation: LabelId for the member.</summary>
    public string? Label { get; init; }
}
