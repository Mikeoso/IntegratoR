using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using FluentAssertions;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.Common.Services;
using IntegratoR.OData.Interfaces.Services;
using IntegratoR.TestKit.Assertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace IntegratoR.OData.Tests.Common.Services;

/// <summary>
/// Pins <see cref="ODataService{TEntity}"/>'s composite-key building behaviour: deterministic
/// <c>[Key]</c> ordering by MetadataToken, a Validation failure (not a silent fallback) on a key
/// count mismatch, and a Validation failure on a null key element — each surfaced BEFORE any
/// adapter call (assert <c>Received(0)</c>).
/// </summary>
public sealed class ODataServiceCompositeKeyTests
{
    [Fact]
    public async Task BuildCompositeKeyObject_OrdersKeyPropertiesByMetadataToken()
    {
        // Arrange — the entity declares its [Key] properties in a known source order; the emitted
        // key dictionary must zip each [Key] wire name to its corresponding value regardless of
        // GetProperties() ordering.
        var adapter = Substitute.For<IODataClientAdapter>();
        object? capturedKey = null;
        adapter.UpdateAsync<ReverseKeyEntity>(
            Arg.Any<string>(),
            Arg.Do<object>(k => capturedKey = k),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>())
            .Returns(new ReverseKeyEntity { First = "1", Second = "2" });

        var service = new ODataService<ReverseKeyEntity>(
            adapter, Substitute.For<ILogger<ODataService<ReverseKeyEntity>>>());

        // GetCompositeKey returns values in declaration order: [First, Second].
        var entity = new ReverseKeyEntity { First = "1210", Second = "LNR0000266" };

        // Act
        await service.UpdateAsync(entity, CancellationToken.None);

        // Assert — declaration order (First then Second) is preserved deterministically.
        var dict = capturedKey.Should().BeAssignableTo<IDictionary<string, object>>().Subject;
        dict["first"].Should().Be("1210");
        dict["second"].Should().Be("LNR0000266");
    }

    [Fact]
    public async Task GetByKeyAsync_KeyCountMismatch_ReturnsValidationFailure()
    {
        // Arrange — TwoKeyEntity declares two [Key] properties; supply three key values.
        var adapter = Substitute.For<IODataClientAdapter>();
        var service = new ODataService<TwoKeyEntity>(
            adapter, Substitute.For<ILogger<ODataService<TwoKeyEntity>>>());

        // Act
        var result = await service.GetByKeyAsync(["a", "b", "c"], CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorType(ErrorType.Validation);
        result.Errors[0].As<IntegrationError>().Code.Should().EndWith(".KeyCountMismatch");
        await adapter.DidNotReceiveWithAnyArgs().FindByKeyAsync<TwoKeyEntity>(
            Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_NullKeyElement_ReturnsValidationFailure()
    {
        // Arrange — entity yields a null second key element, which cannot identify an entity.
        var adapter = Substitute.For<IODataClientAdapter>();
        var service = new ODataService<TwoKeyEntity>(
            adapter, Substitute.For<ILogger<ODataService<TwoKeyEntity>>>());

        var entity = new TwoKeyEntity { First = "1210", Second = null };

        // Act
        var result = await service.UpdateAsync(entity, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorType(ErrorType.Validation);
        result.Errors[0].As<IntegrationError>().Code.Should().EndWith(".InvalidKey");
        await adapter.DidNotReceiveWithAnyArgs().UpdateAsync<TwoKeyEntity>(
            Arg.Any<string>(), Arg.Any<object>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Declares its <c>[Key]</c> properties in source order First → Second so the MetadataToken
    /// ordering test can assert deterministic key-to-value zipping.
    /// </summary>
    [Table("ReverseKeyEntities")]
    public sealed class ReverseKeyEntity : BaseEntity<string>
    {
        [Key]
        [JsonPropertyName("first")]
        public required string First { get; set; }

        [Key]
        [JsonPropertyName("second")]
        public required string Second { get; set; }

        public override object[] GetCompositeKey() => [First, Second];
    }

    /// <summary>
    /// A two-key entity whose <c>GetCompositeKey</c> can yield a null element (Second is nullable),
    /// used to exercise the null-key and count-mismatch Validation failures.
    /// </summary>
    [Table("TwoKeyEntities")]
    public sealed class TwoKeyEntity : BaseEntity<string>
    {
        [Key]
        [JsonPropertyName("first")]
        public required string First { get; set; }

        [Key]
        [JsonPropertyName("second")]
        public string? Second { get; set; }

        public override object[] GetCompositeKey() => [First, Second!];
    }
}
