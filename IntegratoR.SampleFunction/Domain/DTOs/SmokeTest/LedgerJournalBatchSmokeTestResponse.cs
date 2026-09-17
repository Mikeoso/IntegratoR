namespace IntegratoR.SampleFunction.Domain.DTOs.SmokeTest;

/// <summary>
/// Outcome of the LedgerJournalBatchSmokeTest HTTP trigger.
/// </summary>
/// <param name="Success">True only when every batch header was created, found on the server, and deleted again.</param>
/// <param name="RunId">The per-run marker embedded in each header's Description, so orphans left by a failed run can be found in the sandbox.</param>
/// <param name="Steps">Per-step outcomes in execution order.</param>
public sealed record LedgerJournalBatchSmokeTestResponse(
    bool Success,
    string? RunId,
    IReadOnlyList<SmokeTestStep> Steps);
