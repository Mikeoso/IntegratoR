using FluentResults;
using IntegratoR.Abstractions.Interfaces.Queries;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using DimensionFormatModel = IntegratoR.OData.FO.Domain.Models.FinancialDimensions.DimensionFormat;

namespace IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;

public record GetDimensionOrdersQuery(string DimensionFormat, DimensionHierarchyType HierarchyType) : ICacheableQuery<Result<DimensionFormatModel>>
{
    public string CacheKey => $"{nameof(GetDimensionOrdersQuery)}-{DimensionFormat}-{HierarchyType}";

    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(15);

    // GenerateCacheKey/GetCacheKeyValues are [Obsolete] on ICacheableQuery but must still be
    // implemented until the next MAJOR removes the interface members. CachingBehaviour reads CacheKey.
#pragma warning disable CS0618 // Type or member is obsolete
    public string GenerateCacheKey()
    {
        return CacheKey;
    }

    public object[] GetCacheKeyValues()
    {
        return new object[] { nameof(GetDimensionOrdersQuery), DimensionFormat, HierarchyType }
        ;
    }
#pragma warning restore CS0618 // Type or member is obsolete

    public IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return new Dictionary<string, object>
        {
            { "DimensionFormat", DimensionFormat },
            { "HierarchyType", HierarchyType.ToString() }
        };
    }
}
