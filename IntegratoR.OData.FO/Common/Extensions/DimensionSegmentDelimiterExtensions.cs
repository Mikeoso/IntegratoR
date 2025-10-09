using IntegratoR.OData.FO.Domain.Enums.Dimensions;

namespace IntegratoR.OData.FO.Common.Extensions;

public static class DimensionSegmentDelimiterExtensions
{
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
