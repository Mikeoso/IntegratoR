using FluentResults;
using IntegratoR.Abstractions.Interfaces.Queries;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using DimensionFormatModel = IntegratoR.OData.FO.Domain.Models.FinancialDimensions.DimensionFormat;

namespace IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;

/// <summary>
/// Represents a cacheable query for the segment order of a D365 F&amp;O financial dimension format,
/// identified by its <c>DimensionFormat</c> name and <c>HierarchyType</c>.
/// </summary>
/// <param name="DimensionFormat">The name of the dimension format to resolve.</param>
/// <param name="HierarchyType">The hierarchy type that selects the active format.</param>
public record GetDimensionOrdersQuery(string DimensionFormat, DimensionHierarchyType HierarchyType) : ICacheableQuery<Result<DimensionFormatModel>>
{
    /// <summary>Gets the cache key derived from the dimension format name and hierarchy type.</summary>
    public string CacheKey => $"{nameof(GetDimensionOrdersQuery)}-{DimensionFormat}-{HierarchyType}";

    /// <summary>Gets the cache duration for this query.</summary>
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(15);

    // GenerateCacheKey/GetCacheKeyValues are [Obsolete] on ICacheableQuery but must still be
    // implemented until the next MAJOR removes the interface members. CachingBehaviour reads CacheKey.
#pragma warning disable CS0618 // Type or member is obsolete
    /// <inheritdoc/>
    public string GenerateCacheKey()
    {
        return CacheKey;
    }

    /// <inheritdoc/>
    public object[] GetCacheKeyValues()
    {
        return new object[] { nameof(GetDimensionOrdersQuery), DimensionFormat, HierarchyType }
        ;
    }
#pragma warning restore CS0618 // Type or member is obsolete

    /// <summary>Gets the structured logging context for this query.</summary>
    public IReadOnlyDictionary<string, object> GetLoggingContext()
    {
        return new Dictionary<string, object>
        {
            { "DimensionFormat", DimensionFormat },
            { "HierarchyType", HierarchyType.ToString() }
        };
    }
}
