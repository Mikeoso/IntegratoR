using IntegratoR.OData.FO.Domain.Enums.Dimensions;

namespace IntegratoR.OData.FO.Common.Extensions;

/// <summary>
/// Provides extension methods for <see cref="DimensionSegmentDelimiter"/>.
/// </summary>
public static class DimensionSegmentDelimiterExtensions
{
    /// <summary>
    /// Gets the delimiter character corresponding to the specified <see cref="DimensionSegmentDelimiter"/>.
    /// </summary>
    /// <param name="dimensionSegmentDelimiter">The delimiter enum value to map to a character.</param>
    /// <returns>The delimiter character (e.g. <c>'-'</c> for <see cref="DimensionSegmentDelimiter.Hyphen"/>).</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown when <paramref name="dimensionSegmentDelimiter"/> has no mapped delimiter character.
    /// </exception>
    public static char GetCharValue(this DimensionSegmentDelimiter? dimensionSegmentDelimiter)
    {
        switch (dimensionSegmentDelimiter)
        {
            case DimensionSegmentDelimiter.Hyphen:
                return '-';
            default:
                throw new ArgumentOutOfRangeException(nameof(dimensionSegmentDelimiter), dimensionSegmentDelimiter, null);
        }
    }
}
