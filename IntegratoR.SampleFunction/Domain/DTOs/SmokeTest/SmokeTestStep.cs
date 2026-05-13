namespace IntegratoR.SampleFunction.Domain.DTOs.SmokeTest;

/// <summary>
/// Per-step outcome shared across all smoke-test triggers (LedgerJournal, FinancialDimension,
/// future entities). On failure <see cref="ErrorCode"/>, <see cref="ErrorType"/> and
/// <see cref="ErrorMessage"/> carry the <c>IntegrationError</c> details so the caller can
/// diagnose without reading host logs.
/// </summary>
public sealed record SmokeTestStep(
    string Name,
    bool Success,
    string? ErrorCode = null,
    string? ErrorType = null,
    string? ErrorMessage = null,
    string? Details = null);
