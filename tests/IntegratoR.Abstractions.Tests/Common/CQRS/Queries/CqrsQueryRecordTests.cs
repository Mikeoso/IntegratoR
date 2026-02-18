using FluentAssertions;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.TestKit.Doubles.Entities;
using Xunit;

namespace IntegratoR.Abstractions.Tests.Common.CQRS.Queries;

/// <summary>
/// Unit tests for the CQRS query records <see cref="GetByKeyQuery{TEntity}"/> and
/// <see cref="GetByFilterQuery{TEntity}"/> verifying their <c>GetLoggingContext()</c> implementations.
/// </summary>
public sealed class CqrsQueryRecordTests
{
    /// <summary>
    /// Verifies that <see cref="GetByKeyQuery{TEntity}.GetLoggingContext()"/> returns a dictionary
    /// containing the entity type name and a JSON-serialized representation of the composite key.
    /// </summary>
    [Fact]
    public void GetByKeyQuery_GetLoggingContext_ReturnsEntityTypeAndSerializedKeyValues()
    {
        // Arrange
        var keyValues = new object[] { "test-id", "test-partition" };
        var query = new GetByKeyQuery<TestEntity>(keyValues);

        // Act
        var context = query.GetLoggingContext();

        // Assert
        context.Should().ContainKey("EntityType").WhoseValue.Should().Be(nameof(TestEntity));
        context.Should().ContainKey("KeyValues");

        var keyValuesStr = context["KeyValues"].ToString();
        keyValuesStr.Should().Contain("test-id");
        keyValuesStr.Should().Contain("test-partition");
    }

    /// <summary>
    /// Verifies that <see cref="GetByKeyQuery{TEntity}.GetLoggingContext()"/> returns "null"
    /// as the <c>KeyValues</c> entry when <c>CompositeKey</c> is null.
    /// </summary>
    [Fact]
    public void GetByKeyQuery_NullCompositeKey_ReturnsNullStringForKeyValues()
    {
        // Arrange
        var query = new GetByKeyQuery<TestEntity>(null!);

        // Act
        var context = query.GetLoggingContext();

        // Assert
        context.Should().ContainKey("KeyValues");
        context["KeyValues"].ToString().Should().Be("null");
    }

    /// <summary>
    /// Verifies that <see cref="GetByFilterQuery{TEntity}.GetLoggingContext()"/> returns a dictionary
    /// containing the entity type name and the filter expression as a readable string.
    /// </summary>
    [Fact]
    public void GetByFilterQuery_GetLoggingContext_ReturnsEntityTypeAndFilterString()
    {
        // Arrange
        var query = new GetByFilterQuery<TestEntity>(x => x.Id == "test");

        // Act
        var context = query.GetLoggingContext();

        // Assert
        context.Should().ContainKey("EntityType").WhoseValue.Should().Be(nameof(TestEntity));
        context.Should().ContainKey("Filter");

        var filterStr = context["Filter"].ToString();
        filterStr.Should().NotBeNullOrEmpty();
        filterStr.Should().Contain("Id");
        filterStr.Should().Contain("test");
    }
}
