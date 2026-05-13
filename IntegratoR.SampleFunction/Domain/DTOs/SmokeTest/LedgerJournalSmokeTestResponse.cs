namespace IntegratoR.SampleFunction.Domain.DTOs.SmokeTest;

/// <summary>
/// Summary of a LedgerJournal smoke test run. <see cref="Success"/> is true when every
/// step in <see cref="Steps"/> succeeded. If any step fails, the trigger halts forward
/// progress but still best-effort cleans up any records it created — cleanup steps
/// appear in the list as well, flagged with their own success bit.
/// </summary>
public sealed record LedgerJournalSmokeTestResponse(
    bool Success,
    string? CreatedJournalBatchNumber,
    IReadOnlyList<SmokeTestStep> Steps);
