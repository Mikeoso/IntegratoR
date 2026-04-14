namespace IntegratoR.SampleFunction.Domain.DTOs.SmokeTest;

/// <summary>
/// Inputs for the LedgerJournalSmokeTest HTTP trigger. All values are passed from the
/// caller because D365 sandboxes differ (company, journal names, and account display
/// values must reference rows that actually exist in the target environment).
/// </summary>
/// <param name="Company">The D365 legal entity (DataAreaId) to create the journal in.</param>
/// <param name="JournalName">The journal name setup (e.g. "GenJrn").</param>
/// <param name="AccountDisplayValue">Debit account, must exist in the sandbox COA.</param>
/// <param name="OffsetAccountDisplayValue">Credit account, must exist in the sandbox COA.</param>
/// <param name="Amount">Amount to post on both lines (debit + credit).</param>
/// <param name="CurrencyCode">ISO currency code (e.g. "USD", "EUR").</param>
public sealed record LedgerJournalSmokeTestRequest(
    string Company,
    string JournalName,
    string AccountDisplayValue,
    string OffsetAccountDisplayValue,
    decimal Amount,
    string CurrencyCode);
