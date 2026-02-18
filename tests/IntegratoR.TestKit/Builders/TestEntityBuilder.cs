using IntegratoR.TestKit.Doubles.Entities;

namespace IntegratoR.TestKit.Builders;

/// <summary>
/// A fluent builder for <see cref="TestEntity"/> that provides sensible defaults and chainable
/// property overrides to reduce test setup noise.
/// </summary>
public sealed class TestEntityBuilder
{
    private string _id = "test-id";
    private string _partitionKey = "test-partition";
    private string _name = "Test Entity";
    private string? _description;

    /// <summary>
    /// Creates a new <see cref="TestEntityBuilder"/> with default values pre-populated.
    /// </summary>
    /// <returns>A new builder instance with default values.</returns>
    public static TestEntityBuilder Default() => new();

    /// <summary>
    /// Overrides the <see cref="TestEntity.Id"/> value.
    /// </summary>
    /// <param name="id">The identifier to set.</param>
    /// <returns>The current builder instance for chaining.</returns>
    public TestEntityBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Overrides the <see cref="TestEntity.PartitionKey"/> value.
    /// </summary>
    /// <param name="partitionKey">The partition key to set.</param>
    /// <returns>The current builder instance for chaining.</returns>
    public TestEntityBuilder WithPartitionKey(string partitionKey)
    {
        _partitionKey = partitionKey;
        return this;
    }

    /// <summary>
    /// Overrides the <see cref="TestEntity.Name"/> value.
    /// </summary>
    /// <param name="name">The name to set.</param>
    /// <returns>The current builder instance for chaining.</returns>
    public TestEntityBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Overrides the <see cref="TestEntity.Description"/> value.
    /// </summary>
    /// <param name="description">The description to set, or <c>null</c> to clear it.</param>
    /// <returns>The current builder instance for chaining.</returns>
    public TestEntityBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Builds and returns a <see cref="TestEntity"/> using the currently configured values.
    /// </summary>
    /// <returns>A new <see cref="TestEntity"/> instance.</returns>
    public TestEntity Build() => new()
    {
        Id = _id,
        PartitionKey = _partitionKey,
        Name = _name,
        Description = _description
    };
}
