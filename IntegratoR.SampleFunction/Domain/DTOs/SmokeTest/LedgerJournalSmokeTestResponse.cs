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
    IReadOnlyList<LedgerJournalSmokeTestStep> Steps);

/// <summary>
/// Per-step outcome. On failure <see cref="ErrorCode"/> and <see cref="ErrorMessage"/>
/// carry the <c>IntegrationError</c> details so the caller can diagnose without reading logs.
/// </summary>
public sealed record LedgerJournalSmokeTestStep(
    string Name,
    bool Success,
    string? ErrorCode = null,
    string? ErrorType = null,
    string? ErrorMessage = null,
    string? Details = null);
