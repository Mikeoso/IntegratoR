namespace IntegratoR.OData.FO.Domain.Enums.General;

/// <summary>
/// Defines a boolean-like choice between 'No' and 'Yes'.
/// This enumeration corresponds directly to the widely used 'NoYes' base enum in X++ and is the standard
/// way to represent boolean values in Dynamics 365 Finance and Operations data entities.
/// </summary>
public enum NoYes
{
    /// <summary>
    /// Represents the 'No' or false condition, corresponding to the integer value 0.
    /// </summary>
    No = 0,

    /// <summary>
    /// Represents the 'Yes' or true condition, corresponding to the integer value 1.
    /// </summary>
    Yes = 1
}