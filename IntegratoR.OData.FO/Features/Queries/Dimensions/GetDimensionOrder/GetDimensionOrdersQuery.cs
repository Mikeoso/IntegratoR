using IntegratoR.Abstractions.Interfaces.Queries;
using IntegratoR.Abstractions.Interfaces.Results;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;

namespace IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;

public record GetDimensionOrdersQuery(string dimensionFormat, DimensionHierarchyType hierarchyType) : ICacheableQuery<IResult<DimensionFormat>>
{
    public string CacheKey => $"{nameof(GetDimensionOrdersQuery)}-{dimensionFormat}-{hierarchyType}";

    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(15);

    public string GenerateCacheKey()
    {
        return CacheKey;
    }

    public object[] GetCacheKeyValues()
    {
        return new object[] { nameof(GetDimensionOrdersQuery), dimensionFormat, hierarchyType }
        ;
    }
}
