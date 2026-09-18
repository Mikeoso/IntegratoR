namespace IntegratoR.SampleFunction.Domain.DTOs.SmokeTest;

/// <summary>
/// Inputs for the LedgerJournalBatchSmokeTest HTTP trigger, which exercises the <c>$batch</c> write
/// path against a live D365 F&amp;O sandbox.
/// </summary>
/// <param name="Company">The D365 legal entity (DataAreaId) to create the journals in.</param>
/// <param name="JournalName">The journal name setup (e.g. "GenJrn").</param>
/// <param name="HeaderCount">How many headers to create in one batch. Two or more is what makes this a batch; the default keeps the sandbox tidy.</param>
public sealed record LedgerJournalBatchSmokeTestRequest(
    string Company,
    string JournalName,
    int HeaderCount = 3);
