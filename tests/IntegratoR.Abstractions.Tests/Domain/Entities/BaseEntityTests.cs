using FluentAssertions;
using IntegratoR.Abstractions.Domain.Entities;
using Xunit;

namespace IntegratoR.Abstractions.Tests.Domain.Entities;

/// <summary>
/// Unit tests for <see cref="BaseEntity{TKey}"/> covering <c>GetLoggingContext()</c>
/// and <c>GetCompositeKey()</c> behaviours.
/// </summary>
public sealed class BaseEntityTests
{
    /// <summary>
    /// A local test entity with multiple public properties for logging context tests.
    /// </summary>
    private sealed class AllPropertiesEntity : BaseEntity<string>
    {
        /// <summary>Gets or sets the identifier.</summary>
        public required string Id { get; set; }

        /// <summary>Gets or sets the name.</summary>
        public required string Name { get; set; }

        /// <summary>Gets or sets the value.</summary>
        public int Value { get; set; }

        /// <inheritdoc/>
        public override object[] GetCompositeKey() => [Id];
    }

    /// <summary>
    /// A local test entity with a null-valued property to verify null replacement behaviour.
    /// </summary>
    private sealed class NullPropertyEntity : BaseEntity<string>
    {
        /// <summary>Gets or sets the identifier.</summary>
        public required string Id { get; set; }

        /// <summary>Gets or sets a nullable description.</summary>
        public string? Description { get; set; }

        /// <inheritdoc/>
        public override object[] GetCompositeKey() => [Id];
    }

    /// <summary>
    /// A local test entity with an indexed property to verify indexer exclusion.
    /// </summary>
    private sealed class IndexedPropertyEntity : BaseEntity<string>
    {
        private readonly Dictionary<int, string> _data = [];

        /// <summary>Gets or sets the identifier.</summary>
        public required string Id { get; set; }

        /// <summary>Gets or sets a value by index.</summary>
        public string this[int index]
        {
            get => _data.TryGetValue(index, out var v) ? v : string.Empty;
            set => _data[index] = value;
        }

        /// <inheritdoc/>
        public override object[] GetCompositeKey() => [Id];
    }

    /// <summary>
    /// A local test entity with no public instance properties beyond the base class contract.
    /// </summary>
    private sealed class NoPublicPropertiesEntity : BaseEntity<string>
    {
        /// <inheritdoc/>
        public override object[] GetCompositeKey() => ["key"];
    }

    /// <summary>
    /// A local test entity with composite key for verifying <c>GetCompositeKey()</c>.
    /// </summary>
    private sealed class CompositeKeyEntity : BaseEntity<string>
    {
        /// <summary>Gets or sets the identifier.</summary>
        public required string Id { get; set; }

        /// <summary>Gets or sets the partition key.</summary>
        public required string PartitionKey { get; set; }

        /// <inheritdoc/>
        public override object[] GetCompositeKey() => [Id, PartitionKey];
    }

    /// <summary>
    /// Verifies that <c>GetLoggingContext()</c> returns a dictionary containing all public instance properties.
    /// </summary>
    [Fact]
    public void GetLoggingContext_AllPropertiesPopulated_ReturnsDictionaryWithAllPublicProperties()
    {
        // Arrange
        var entity = new AllPropertiesEntity { Id = "abc", Name = "Test", Value = 42 };

        // Act
        var context = entity.GetLoggingContext();

        // Assert
        context.Should().ContainKey("Id").WhoseValue.Should().Be("abc");
        context.Should().ContainKey("Name").WhoseValue.Should().Be("Test");
        context.Should().ContainKey("Value").WhoseValue.Should().Be(42);
    }

    /// <summary>
    /// Verifies that null property values are replaced with a new <see cref="object"/> instance.
    /// </summary>
    [Fact]
    public void GetLoggingContext_NullPropertyValue_ReplacesWithNewObject()
    {
        // Arrange
        var entity = new NullPropertyEntity { Id = "x", Description = null };

        // Act
        var context = entity.GetLoggingContext();

        // Assert
        context.Should().ContainKey("Description");
        context["Description"].Should().NotBeNull();
        context["Description"].Should().BeOfType<object>();
    }

    /// <summary>
    /// Verifies that indexed properties are excluded from the logging context.
    /// </summary>
    [Fact]
    public void GetLoggingContext_EntityWithIndexedProperty_ExcludesIndexer()
    {
        // Arrange
        var entity = new IndexedPropertyEntity { Id = "id-1" };

        // Act
        var context = entity.GetLoggingContext();

        // Assert
        context.Should().ContainKey("Id");
        context.Keys.Should().NotContain("Item", "indexed properties should be excluded from logging context");
    }

    /// <summary>
    /// Verifies that an entity with no public instance properties returns an empty dictionary.
    /// </summary>
    [Fact]
    public void GetLoggingContext_EntityWithNoPublicProperties_ReturnsEmptyDictionary()
    {
        // Arrange
        var entity = new NoPublicPropertiesEntity();

        // Act
        var context = entity.GetLoggingContext();

        // Assert
        context.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that <c>GetCompositeKey()</c> returns the correct array of key values.
    /// </summary>
    [Fact]
    public void GetCompositeKey_CompositeKeyEntity_ReturnsCorrectKeyArray()
    {
        // Arrange
        var entity = new CompositeKeyEntity { Id = "id-123", PartitionKey = "pk-456" };

        // Act
        var key = entity.GetCompositeKey();

        // Assert
        key.Should().HaveCount(2);
        key[0].Should().Be("id-123");
        key[1].Should().Be("pk-456");
    }
}
