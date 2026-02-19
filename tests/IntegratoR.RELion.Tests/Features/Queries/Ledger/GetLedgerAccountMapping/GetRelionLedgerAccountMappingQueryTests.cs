using FluentAssertions;
using IntegratoR.RELion.Features.Queries.Ledger.GetLedgerAccountMapping;
using Xunit;

namespace IntegratoR.RELion.Tests.Features.Queries.Ledger.GetLedgerAccountMapping;

/// <summary>
/// Tests for <see cref="GetRelionLedgerAccountMappingQuery"/> record behaviour,
/// covering cache key generation, cache duration, and logging context.
/// </summary>
public class GetRelionLedgerAccountMappingQueryTests
{
    /// <summary>
    /// Verifies that CacheKey returns the EntryNo as a string.
    /// </summary>
    [Fact]
    public void CacheKey_ContainsEntryNo()
    {
        // Arrange
        var query = new GetRelionLedgerAccountMappingQuery(42);

        // Act
        var cacheKey = query.CacheKey;

        // Assert
        cacheKey.Should().Be("42");
    }

    /// <summary>
    /// Verifies that CacheDuration is 30 minutes.
    /// </summary>
    [Fact]
    public void CacheDuration_Is30Minutes()
    {
        // Arrange
        var query = new GetRelionLedgerAccountMappingQuery(1);

        // Act
        var duration = query.CacheDuration;

        // Assert
        duration.Should().Be(TimeSpan.FromMinutes(30));
    }

    /// <summary>
    /// Verifies that GetLoggingContext returns a dictionary containing the EntryNo key with the correct value.
    /// </summary>
    [Fact]
    public void GetLoggingContext_ContainsEntryNo()
    {
        // Arrange
        var query = new GetRelionLedgerAccountMappingQuery(42);

        // Act
        var context = query.GetLoggingContext();

        // Assert
        context.Should().ContainKey("EntryNo");
        context["EntryNo"].Should().Be(42);
    }
}
