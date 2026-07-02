namespace IntegratoR.SampleFunction.Domain.DTOs.SmokeTest;

/// <summary>
/// Inputs for the LedgerJournalBatchSmokeTest HTTP trigger, which exercises the configurable,
/// chunked <c>$batch</c> write path (v3.0.0) against a live D365 F&amp;O sandbox: chunked atomic
/// create, atomic-changeset rollback, continue-on-error partial accept, and batch delete.
/// </summary>
/// <param name="Company">The D365 legal entity (DataAreaId) to create the journal in.</param>
/// <param name="JournalName">The journal name setup (e.g. "GenJrn").</param>
/// <param name="AccountDisplayValue">Debit account, must exist in the sandbox COA.</param>
/// <param name="OffsetAccountDisplayValue">Credit account, must exist in the sandbox COA.</param>
/// <param name="Amount">Amount posted on each line.</param>
/// <param name="CurrencyCode">ISO currency code (e.g. "USD", "EUR").</param>
/// <param name="LineCount">
/// How many lines to batch-create (default 6). Must be at least 2 so the update tests have
/// distinct lines to target.
/// </param>
/// <param name="ChunkSize">
/// The per-chunk operation cap for the create step (default 2), set deliberately small so the
/// create splits across several <c>$batch</c> changesets and the chunking path is exercised.
/// </param>
public sealed record LedgerJournalBatchSmokeTestRequest(
    string Company,
    string JournalName,
    string AccountDisplayValue,
    string OffsetAccountDisplayValue,
    decimal Amount,
    string CurrencyCode,
    int LineCount = 6,
    int ChunkSize = 2);
