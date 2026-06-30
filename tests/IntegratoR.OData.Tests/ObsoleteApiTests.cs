using System.Reflection;
using FluentAssertions;
using IntegratoR.OData.Common.Exceptions;
using IntegratoR.OData.Common.Services;
using IntegratoR.OData.Interfaces.Services;
using Xunit;

namespace IntegratoR.OData.Tests;

/// <summary>
/// Reflection pins that assert the deprecation markers on public OData API remain in place.
/// These guard against premature removal of an <see cref="ObsoleteAttribute"/> before the next MAJOR.
/// </summary>
public sealed class ObsoleteApiTests
{
    /// <summary>
    /// <see cref="ODataBatchException"/> is never thrown (batch failures surface via <c>Result&lt;T&gt;</c>)
    /// and must stay <c>[Obsolete]</c> until removed next MAJOR.
    /// </summary>
    [Fact]
    public void ODataBatchException_IsMarkedObsolete()
    {
#pragma warning disable CS0618 // Type or member is obsolete — pinning the deprecation marker
        ObsoleteAttribute? obsolete = typeof(ODataBatchException).GetCustomAttribute<ObsoleteAttribute>();
#pragma warning restore CS0618

        obsolete.Should().NotBeNull();
        obsolete!.Message.Should().Contain("Result");
    }

    /// <summary>
    /// The dead, never-injected <see cref="ODataMetadataProvider"/> must stay <c>[Obsolete]</c>
    /// until removed next MAJOR.
    /// </summary>
    [Fact]
    public void ODataMetadataProvider_IsMarkedObsolete()
    {
#pragma warning disable CS0618 // Type or member is obsolete — pinning the deprecation marker
        ObsoleteAttribute? obsolete = typeof(ODataMetadataProvider).GetCustomAttribute<ObsoleteAttribute>();
#pragma warning restore CS0618

        obsolete.Should().NotBeNull();
        obsolete!.Message.Should().Contain("D365 $metadata");
    }

    /// <summary>
    /// <see cref="IODataService{TEntity}.FindAll"/> must stay <c>[Obsolete]</c> (callers should use
    /// <c>FindAllAsync</c>) until removed next MAJOR.
    /// </summary>
    [Fact]
    public void FindAll_IsMarkedObsolete()
    {
        MethodInfo? method = typeof(IODataService<>).GetMethod("FindAll");

        method.Should().NotBeNull();
        ObsoleteAttribute? obsolete = method!.GetCustomAttribute<ObsoleteAttribute>();
        obsolete.Should().NotBeNull();
        obsolete!.Message.Should().Contain("FindAllAsync");
    }
}
