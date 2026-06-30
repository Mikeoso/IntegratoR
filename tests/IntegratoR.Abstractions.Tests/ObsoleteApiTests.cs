using System.Reflection;
using FluentAssertions;
using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.Abstractions.Interfaces.Queries;
using Xunit;

namespace IntegratoR.Abstractions.Tests;

/// <summary>
/// Reflection pins that assert the deprecation markers on public Abstractions API remain in place.
/// These guard against premature removal of an <see cref="ObsoleteAttribute"/> before the next MAJOR.
/// </summary>
public sealed class ObsoleteApiTests
{
    /// <summary>
    /// <see cref="BaseEntity{TKey}"/> must stay <c>[Obsolete]</c> (the non-generic
    /// <see cref="BaseEntity"/> is the supported base) until removed next MAJOR.
    /// </summary>
    [Fact]
    public void GenericBaseEntity_IsMarkedObsolete()
    {
#pragma warning disable CS0618 // Type or member is obsolete — pinning the deprecation marker
        ObsoleteAttribute? obsolete = typeof(BaseEntity<>).GetCustomAttribute<ObsoleteAttribute>();
#pragma warning restore CS0618

        obsolete.Should().NotBeNull();
        obsolete!.Message.Should().Contain("non-generic BaseEntity");
    }

    /// <summary>
    /// <see cref="ICacheableQuery{TResponse}.GenerateCacheKey"/> must stay <c>[Obsolete]</c>;
    /// the caching pipeline reads <c>CacheKey</c> directly.
    /// </summary>
    [Fact]
    public void GenerateCacheKey_IsMarkedObsolete()
    {
        MethodInfo? method = typeof(ICacheableQuery<>).GetMethod(nameof(ICacheableQuery<object>.GenerateCacheKey));

        method.Should().NotBeNull();
        ObsoleteAttribute? obsolete = method!.GetCustomAttribute<ObsoleteAttribute>();
        obsolete.Should().NotBeNull();
        obsolete!.Message.Should().Contain("CacheKey");
    }

    /// <summary>
    /// <see cref="ICacheableQuery{TResponse}.GetCacheKeyValues"/> must stay <c>[Obsolete]</c>;
    /// the caching pipeline reads <c>CacheKey</c> directly.
    /// </summary>
    [Fact]
    public void GetCacheKeyValues_IsMarkedObsolete()
    {
        MethodInfo? method = typeof(ICacheableQuery<>).GetMethod(nameof(ICacheableQuery<object>.GetCacheKeyValues));

        method.Should().NotBeNull();
        ObsoleteAttribute? obsolete = method!.GetCustomAttribute<ObsoleteAttribute>();
        obsolete.Should().NotBeNull();
        obsolete!.Message.Should().Contain("CacheKey");
    }
}
