namespace IntegratoR.OData.FO.Domain.Enums.Dimensions;

/// <summary>
/// Specifies the character or character sequence used to separate the segments within a financial dimension string.
/// This is a critical configuration for correctly parsing and constructing dimension values for integration with D365 F&O.
/// For example, in the string "618160-001-023", the delimiter is a Hyphen.
/// </summary>
public enum DimensionSegmentDelimiter
{
    /// <summary>
    /// Represents a single hyphen character ('-') as the delimiter.
    /// </summary>
    Hyphen = 0,

    /// <summary>
    /// Represents a single period character ('.') as the delimiter.
    /// </summary>
    Period = 1,

    /// <summary>
    /// Represents a single underscore character ('_') as the delimiter.
    /// </summary>
    Underscore = 2,

    /// <summary>
    /// Represents a single vertical bar or pipe character ('|') as the delimiter.
    /// </summary>
    Bar = 3,

    /// <summary>
    /// Represents a double hyphen ('--') as the delimiter.
    /// </summary>
    DoubleHypen = 4,

    /// <summary>
    /// Represents a double period ('..') as the delimiter.
    /// </summary>
    DoublePeriod = 5,

    /// <summary>
    /// Represents a double underscore ('__') as the delimiter.
    /// </summary>
    DoubleUnderscore = 6,

    /// <summary>
    /// Represents a double vertical bar or pipe ('||') as the delimiter.
    /// </summary>
    DoubleBar = 7,

    /// <summary>
    /// Represents a single tilde character ('~') as the delimiter.
    /// </summary>
    Tilde = 8,

    /// <summary>
    /// Represents a double tilde ('~~') as the delimiter.
    /// </summary>
    DoubleTilde = 9
}