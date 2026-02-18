using FluentAssertions;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;
using Xunit;

namespace IntegratoR.OData.FO.Tests.Features.Queries.Dimensions.GetDimensionOrder;

/// <summary>
/// Tests for <see cref="GetDimensionOrdersQuery"/> record verifying cache key, cache duration, and logging context.
/// </summary>
public class GetDimensionOrdersQueryTests
{
    /// <summary>
    /// Verifies that CacheKey contains the query name, dimension format, and hierarchy type.
    /// </summary>
    [Fact]
    public void CacheKey_ContainsQueryNameFormatAndHierarchyType()
    {
        // Arrange
        var query = new GetDimensionOrdersQuery(
            "DefaultFormat",
            DimensionHierarchyType.DataEntityDefaultDimensionFormat);

        // Act
        var cacheKey = query.CacheKey;

        // Assert
        cacheKey.Should().Be("GetDimensionOrdersQuery-DefaultFormat-DataEntityDefaultDimensionFormat");
    }

    /// <summary>
    /// Verifies that CacheDuration is 15 minutes.
    /// </summary>
    [Fact]
    public void CacheDuration_Is15Minutes()
    {
        // Arrange
        var query = new GetDimensionOrdersQuery(
            "DefaultFormat",
            DimensionHierarchyType.DataEntityDefaultDimensionFormat);

        // Act
        var duration = query.CacheDuration;

        // Assert
        duration.Should().Be(TimeSpan.FromMinutes(15));
    }

    /// <summary>
    /// Verifies that GetLoggingContext returns a dictionary with DimensionFormat and HierarchyType keys and correct values.
    /// </summary>
    [Fact]
    public void GetLoggingContext_ContainsDimensionFormatAndHierarchyType()
    {
        // Arrange
        var query = new GetDimensionOrdersQuery(
            "DefaultFormat",
            DimensionHierarchyType.DataEntityDefaultDimensionFormat);

        // Act
        var context = query.GetLoggingContext();

        // Assert
        context.Should().ContainKey("DimensionFormat");
        context.Should().ContainKey("HierarchyType");
        context["DimensionFormat"].Should().Be("DefaultFormat");
        context["HierarchyType"].Should().Be(DimensionHierarchyType.DataEntityDefaultDimensionFormat.ToString());
    }
}
