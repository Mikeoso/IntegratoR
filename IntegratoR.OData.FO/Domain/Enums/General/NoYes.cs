namespace IntegratoR.OData.FO.Domain.Enums.General;

/// <summary>
/// Defines a boolean-like choice between No and Yes, mapping to the <c>NoYes</c> X++ base enum used for boolean values in D365 F&amp;O data entities.
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
