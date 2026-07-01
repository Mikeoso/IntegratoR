namespace IntegratoR.OData.FO.Domain.Enums.LedgerJournals;

/// <summary>
/// Defines the posting layer for a financial transaction in D365 F&amp;O, enabling parallel accounting entries for a single business event, and maps to the <c>CurrentOperationsTax</c> X++ base enum.
/// </summary>
public enum CurrentOperationsTax
{
    /// <summary>
    /// Represents the primary, operational posting layer used for day-to-day accounting and statutory reporting.
    /// </summary>
    Current = 0,

    /// <summary>
    /// A secondary layer, often used for internal management reporting or adjustments that should not affect the official books.
    /// </summary>
    Operations = 1,

    /// <summary>
    /// A specific layer used for tax-related transactions and adjustments, providing a separate view for tax reporting.
    /// </summary>
    Tax = 2,

    /// <summary>
    /// Represents the absence of a specific posting layer.
    /// </summary>
    None = 3,

    /// <summary>
    /// A country/region-specific posting layer for warehouse currency transactions (for Russia).
    /// </summary>
    WarehouseCur_RU = 17,

    /// <summary>
    /// Represents the first user-definable custom posting layer, configurable within D365 F&amp;O.
    /// </summary>
    CustomLayer1 = 18,

    /// <summary>
    /// Represents the second user-definable custom posting layer, configurable within D365 F&amp;O.
    /// </summary>
    CustomLayer2 = 19,

    /// <summary>
    /// Represents the third user-definable custom posting layer, configurable within D365 F&amp;O.
    /// </summary>
    CustomLayer3 = 20,

    /// <summary>
    /// Represents the fourth user-definable custom posting layer, configurable within D365 F&amp;O.
    /// </summary>
    CustomLayer4 = 21,

    /// <summary>
    /// Represents the fifth user-definable custom posting layer, configurable within D365 F&amp;O.
    /// </summary>
    CustomLayer5 = 22,

    /// <summary>
    /// Represents the sixth user-definable custom posting layer, configurable within D365 F&amp;O.
    /// </summary>
    CustomLayer6 = 23,

    /// <summary>
    /// Represents the seventh user-definable custom posting layer, configurable within D365 F&amp;O.
    /// </summary>
    CustomLayer7 = 24
}
