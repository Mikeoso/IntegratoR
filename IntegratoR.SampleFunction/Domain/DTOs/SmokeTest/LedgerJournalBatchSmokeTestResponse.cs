namespace IntegratoR.SampleFunction.Domain.DTOs.SmokeTest;

/// <summary>
/// Result of the LedgerJournalBatchSmokeTest HTTP trigger. <see cref="Success"/> is <c>true</c>
/// only when every step succeeded; <see cref="Steps"/> carries the per-step outcome so a caller
/// can see exactly which batch phase (chunked create, atomic rollback, continue-on-error partial,
/// batch delete) passed or failed.
/// </summary>
public sealed record LedgerJournalBatchSmokeTestResponse(
    bool Success,
    string? CreatedJournalBatchNumber,
    IReadOnlyList<SmokeTestStep> Steps);
