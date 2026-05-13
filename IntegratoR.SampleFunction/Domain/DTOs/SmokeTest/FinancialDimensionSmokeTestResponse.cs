namespace IntegratoR.SampleFunction.Domain.DTOs.SmokeTest;

/// <summary>
/// Summary of a financial-dimension smoke test run. Exercises the
/// <c>GetDimensionOrdersQuery</c> MediatR handler end-to-end against a live D365 F&amp;O
/// sandbox so consumers can confirm the custom query handler, the underlying generic
/// <c>IODataService&lt;DimensionParameters&gt;</c> and <c>IService&lt;DimensionIntegrationFormat&gt;</c>
/// services, and the camelCase filter translator all work together on a read path that
/// does NOT require a company context (the dimension metadata entities are global).
/// </summary>
public sealed record FinancialDimensionSmokeTestResponse(
    bool Success,
    string? Delimiter,
    IReadOnlyList<string>? Segments,
    IReadOnlyList<LedgerJournalSmokeTestStep> Steps);
