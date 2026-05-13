using IntegratoR.OData.FO.Domain.Enums.Dimensions;

namespace IntegratoR.SampleFunction.Domain.DTOs.SmokeTest;

/// <summary>
/// Inputs for the FinancialDimensionSmokeTest HTTP trigger. Values are passed from the
/// caller because D365 sandboxes differ — the <c>DimensionFormatName</c> and
/// <c>HierarchyType</c> must reference a <c>DimensionIntegrationFormat</c> row that
/// actually exists in the target environment. No company context is required because
/// the dimension metadata entities are global (not per-<c>DataAreaId</c>).
/// </summary>
/// <param name="DimensionFormatName">
/// The name of the <c>DimensionIntegrationFormat</c> configuration to look up (e.g.
/// <c>"Sachkontodimensionen"</c>).
/// </param>
/// <param name="HierarchyType">
/// The <see cref="DimensionHierarchyType"/> that combined with <paramref name="DimensionFormatName"/>
/// uniquely identifies the format row. Typical value for ledger dimension formats:
/// <see cref="DimensionHierarchyType.DataEntityLedgerDimensionFormat"/>.
/// </param>
public sealed record FinancialDimensionSmokeTestRequest(
    string DimensionFormatName,
    DimensionHierarchyType HierarchyType);
