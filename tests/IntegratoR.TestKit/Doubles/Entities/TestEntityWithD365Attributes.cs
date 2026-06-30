using System.ComponentModel.DataAnnotations;
using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.Common.Annotations;

namespace IntegratoR.TestKit.Doubles.Entities;

/// <summary>
/// A test entity using D365 CSDL-derived <see cref="ODataFieldAttribute"/> properties
/// (<see cref="ODataFieldAttribute.AllowEdit"/>, <see cref="ODataFieldAttribute.AllowEditOnCreate"/>,
/// <see cref="ODataFieldAttribute.IsRequired"/>) to verify that <c>ODataService</c> enforces
/// the extended attribute semantics at runtime.
/// </summary>
public class TestEntityWithD365Attributes : BaseEntity<string>
{
    /// <summary>
    /// Gets or sets the data area (company). Required, editable on create but not after.
    /// First component of the composite key.
    /// </summary>
    [Key]
    [ODataField(AllowEdit = false, IsRequired = true, EdmType = "Edm.String")]
    public required string DataAreaId { get; set; }

    /// <summary>
    /// Gets or sets the journal number. Not editable on create (server-generated) or update.
    /// Second component of the composite key.
    /// </summary>
    [Key]
    [ODataField(AllowEditOnCreate = false, AllowEdit = false, EdmType = "Edm.String")]
    public string? JournalBatchNumber { get; set; }

    /// <summary>
    /// Gets or sets the description. Fully editable, not required.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the journal name. Required by D365 but nullable in C# to allow testing validation.
    /// </summary>
    [ODataField(IsRequired = true, EdmType = "Edm.String")]
    public string? JournalName { get; set; }

    /// <summary>
    /// Gets or sets the amount. Required field.
    /// </summary>
    [ODataField(IsRequired = true, EdmType = "Edm.Decimal")]
    public required decimal Amount { get; set; }

    /// <inheritdoc/>
    public override object[] GetCompositeKey() => [DataAreaId, JournalBatchNumber!];
}
