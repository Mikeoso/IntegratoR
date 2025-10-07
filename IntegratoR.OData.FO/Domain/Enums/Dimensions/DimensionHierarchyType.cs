namespace IntegratoR.OData.FO.Domain.Enums.Dimensions;

/// <summary>
/// Defines the different types of dimension hierarchies and validation structures used in Dynamics 365 Finance and Operations.
/// This enumeration corresponds directly to the 'DimensionHierarchyType' base enum in X++ and is fundamental for
/// dimension-related development and integration. It determines which validation rules, storage patterns, and account
/// structures are applied to a set of financial dimensions.
/// </summary>
public enum DimensionHierarchyType
{
    /// <summary>
    /// Represents the primary chart of accounts structure that defines the allowed combinations of main accounts and financial dimensions.
    /// </summary>
    AccountStructure = 0,

    /// <summary>
    /// Represents an advanced rule structure that adds constraints to an existing account structure.
    /// </summary>
    AccountRuleStructure = 1,

    /// <summary>
    /// Represents a dimension structure used for journal control setup to enforce specific dimension values on journals.
    /// </summary>
    JournalControlStructure = 2,

    /// <summary>
    /// Represents a dimension structure used in budgeting and planning focuses.
    /// </summary>
    Focus = 6,

    /// <summary>
    /// Represents the financial dimensions linked to a Customer master record.
    /// </summary>
    Customer = 7,

    /// <summary>
    /// Represents the financial dimensions linked to a Vendor master record.
    /// </summary>
    Vendor = 8,

    /// <summary>
    /// Represents the financial dimensions linked to a Project record.
    /// </summary>
    Project = 9,

    /// <summary>
    /// Represents the financial dimensions linked to a Fixed Asset record.
    /// </summary>
    FixedAsset = 10,

    /// <summary>
    /// Represents the financial dimensions linked to a Bank Account record.
    /// </summary>
    BankAccount = 11,

    /// <summary>
    /// Represents the financial dimensions linked to an Employee or Worker record.
    /// </summary>
    Employee = 12,

    /// <summary>
    /// Represents the financial dimensions linked to an Item master record.
    /// </summary>
    Item = 13,

    /// <summary>
    /// A generic structure for a single financial dimension attribute.
    /// </summary>
    SingleAttributeStructure = 14,

    /// <summary>
    /// Represents the structure for a default account, typically used in posting definitions.
    /// </summary>
    DefaultAccount = 16,

    /// <summary>
    /// A country/region-specific dimension structure for fixed assets (for Russia).
    /// </summary>
    FixedAssets_RU = 101,

    /// <summary>
    /// A country/region-specific dimension structure for deferrals (for Russia).
    /// </summary>
    RDeferrals = 102,

    /// <summary>
    /// A country/region-specific dimension structure for cash accounts (for Russia).
    /// </summary>
    RCash = 103,

    /// <summary>
    /// A country/region-specific dimension structure for employees (for Russia).
    /// </summary>
    Employee_RU = 104,

    /// <summary>
    /// A generic structure that includes all financial dimension attributes.
    /// </summary>
    AllAttributeStructure = 105,

    /// <summary>
    /// Specifies the format for a 'Default dimension' (without a main account) used in data entities.
    /// </summary>
    DataEntityDefaultDimensionFormat = 17,

    /// <summary>
    /// Specifies the format for a 'Ledger dimension' (main account + dimensions) used in data entities.
    /// </summary>
    DataEntityLedgerDimensionFormat = 18,

    /// <summary>
    /// Specifies the format for a budget dimension used in data entities.
    /// </summary>
    DataEntityBudgetDimensionFormat = 19,

    /// <summary>
    /// Specifies the format for a budget planning dimension used in data entities.
    /// </summary>
    DataEntityBudgetPlanningDimensionFormat = 20,

    /// <summary>
    /// Represents the dimension format used in Financial Reporting (formerly Management Reporter).
    /// </summary>
    FinancialReportingDimensionFormat = 106,

    /// <summary>
    /// Represents a dimension that is derived from other dimension values.
    /// </summary>
    DerivedDimension = 107,

    /// <summary>
    /// Represents the dimension structure used for cash flow forecasting.
    /// </summary>
    CashFlowForecast = 108
}