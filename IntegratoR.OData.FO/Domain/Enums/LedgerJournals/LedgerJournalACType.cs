namespace IntegratoR.OData.FO.Domain.Enums.LedgerJournals;

/// <summary>
/// Defines the account type for a ledger journal line in Dynamics 365 Finance and Operations.
/// This value determines the business logic, validation, and underlying tables used for the 'Account'
/// and 'OffsetAccount' fields on the journal line. It corresponds directly to the 'LedgerJournalACType' base enum in X++.
/// </summary>
public enum LedgerJournalACType
{
    /// <summary>
    /// Represents a main account from the general ledger (chart of accounts).
    /// </summary>
    Ledger = 0,

    /// <summary>
    /// Represents a customer account. The transaction will be posted to the customer's subledger.
    /// </summary>
    Cust = 1,

    /// <summary>
    /// Represents a vendor account. The transaction will be posted to the vendor's subledger.
    /// </summary>
    Vend = 2,

    /// <summary>
    /// Represents a project account from the Project Management and Accounting module.
    /// </summary>
    Project = 3,

    /// <summary>
    /// Represents a fixed asset account. The transaction will be posted to the fixed assets subledger.
    /// </summary>
    FixedAssets = 5,

    /// <summary>
    /// Represents a bank account.
    /// </summary>
    Bank = 6,

    /// <summary>
    /// Represents a fixed asset account, a feature specific to the Russian localization.
    /// </summary>
    FixedAssets_RU = 12,

    /// <summary>
    /// Represents an employee account, a feature specific to the Russian localization.
    /// </summary>
    Employee_RU = 13,

    /// <summary>
    /// Represents a deferrals account, a feature specific to the Russian localization.
    /// </summary>
    RDeferrals = 14,

    /// <summary>
    /// Represents a cash account, a feature specific to the Russian localization.
    /// </summary>
    RCash = 15
}