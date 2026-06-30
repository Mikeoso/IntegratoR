using FluentAssertions;
using IntegratoR.Abstractions.Domain.Entities;
using Xunit;

namespace IntegratoR.Abstractions.Tests.Domain.Entities;

/// <summary>
/// Unit tests proving the NEW non-generic <see cref="BaseEntity"/> behaves identically to the
/// (now obsolete) generic <c>BaseEntity&lt;TKey&gt;</c> for <c>GetCompositeKey()</c> and
/// <c>GetLoggingContext()</c>.
/// </summary>
public sealed class BaseEntityNonGenericTests
{
    /// <summary>
    /// A composite-key entity inheriting the non-generic <see cref="BaseEntity"/> directly.
    /// </summary>
    private sealed class NonGenericCompositeEntity : BaseEntity
    {
        /// <summary>Gets or sets the data area (first key component).</summary>
        public required string DataAreaId { get; set; }

        /// <summary>Gets or sets the business key (second key component).</summary>
        public required string DocumentNumber { get; set; }

        /// <summary>Gets or sets a non-key descriptive field.</summary>
        public string? Description { get; set; }

        /// <inheritdoc/>
        public override object[] GetCompositeKey() => [DataAreaId, DocumentNumber];
    }

    /// <summary>
    /// Verifies <c>GetCompositeKey()</c> returns the expected ordered key array.
    /// </summary>
    [Fact]
    public void GetCompositeKey_ReturnsExpectedOrderedArray()
    {
        // Arrange
        var entity = new NonGenericCompositeEntity
        {
            DataAreaId = "USMF",
            DocumentNumber = "DOC-001",
            Description = "ignored for key"
        };

        // Act
        object[] key = entity.GetCompositeKey();

        // Assert
        key.Should().HaveCount(2);
        key[0].Should().Be("USMF");
        key[1].Should().Be("DOC-001");
    }

    /// <summary>
    /// Verifies <c>GetLoggingContext()</c> contains the entity's public properties (inherited from
    /// the non-generic base) with their values.
    /// </summary>
    [Fact]
    public void GetLoggingContext_ContainsPublicProperties()
    {
        // Arrange
        var entity = new NonGenericCompositeEntity
        {
            DataAreaId = "USMF",
            DocumentNumber = "DOC-001",
            Description = "ledger import"
        };

        // Act
        var context = entity.GetLoggingContext();

        // Assert
        context.Should().ContainKey("DataAreaId").WhoseValue.Should().Be("USMF");
        context.Should().ContainKey("DocumentNumber").WhoseValue.Should().Be("DOC-001");
        context.Should().ContainKey("Description").WhoseValue.Should().Be("ledger import");
    }
}
