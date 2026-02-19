using FluentAssertions;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.TestKit.Builders;
using Xunit;

namespace IntegratoR.Abstractions.Tests.Common.CQRS.Commands;

/// <summary>
/// Unit tests for all CQRS command records verifying that <c>GetLoggingContext()</c>
/// delegates to the entity or returns a count for batch commands.
/// </summary>
public sealed class CqrsCommandRecordTests
{
    /// <summary>
    /// Verifies that <see cref="CreateCommand{TEntity}.GetLoggingContext()"/> delegates
    /// to the entity's <c>GetLoggingContext()</c>.
    /// </summary>
    [Fact]
    public void CreateCommand_GetLoggingContext_DelegatesToEntityGetLoggingContext()
    {
        // Arrange
        var entity = TestEntityBuilder.Default().WithId("c-001").WithName("Create Test").Build();
        var command = new CreateCommand<IntegratoR.TestKit.Doubles.Entities.TestEntity>(entity);

        // Act
        var context = command.GetLoggingContext();

        // Assert
        context.Should().ContainKey("Id").WhoseValue.Should().Be("c-001");
        context.Should().ContainKey("Name").WhoseValue.Should().Be("Create Test");
    }

    /// <summary>
    /// Verifies that <see cref="CreateBatchCommand{TEntity}.GetLoggingContext()"/> returns
    /// a dictionary containing the <c>Count</c> of entities in the batch.
    /// </summary>
    [Fact]
    public void CreateBatchCommand_GetLoggingContext_ReturnsDictionaryWithCount()
    {
        // Arrange
        var entities = new[]
        {
            TestEntityBuilder.Default().WithId("b-001").Build(),
            TestEntityBuilder.Default().WithId("b-002").Build(),
            TestEntityBuilder.Default().WithId("b-003").Build()
        };
        var command = new CreateBatchCommand<IntegratoR.TestKit.Doubles.Entities.TestEntity>(entities);

        // Act
        var context = command.GetLoggingContext();

        // Assert
        context.Should().ContainKey("Count").WhoseValue.Should().Be(3);
    }

    /// <summary>
    /// Verifies that <see cref="UpdateCommand{TEntity}.GetLoggingContext()"/> delegates
    /// to the entity's <c>GetLoggingContext()</c>.
    /// </summary>
    [Fact]
    public void UpdateCommand_GetLoggingContext_DelegatesToEntity()
    {
        // Arrange
        var entity = TestEntityBuilder.Default().WithId("u-001").WithName("Update Test").Build();
        var command = new UpdateCommand<IntegratoR.TestKit.Doubles.Entities.TestEntity>(entity);

        // Act
        var context = command.GetLoggingContext();

        // Assert
        context.Should().ContainKey("Id").WhoseValue.Should().Be("u-001");
        context.Should().ContainKey("Name").WhoseValue.Should().Be("Update Test");
    }

    /// <summary>
    /// Verifies that <see cref="UpdateBatchCommand{TEntity}.GetLoggingContext()"/> returns
    /// a dictionary containing the <c>Count</c> of entities in the batch.
    /// </summary>
    [Fact]
    public void UpdateBatchCommand_GetLoggingContext_ReturnsDictionaryWithCount()
    {
        // Arrange
        var entities = new[]
        {
            TestEntityBuilder.Default().WithId("ub-001").Build(),
            TestEntityBuilder.Default().WithId("ub-002").Build()
        };
        var command = new UpdateBatchCommand<IntegratoR.TestKit.Doubles.Entities.TestEntity>(entities);

        // Act
        var context = command.GetLoggingContext();

        // Assert
        context.Should().ContainKey("Count").WhoseValue.Should().Be(2);
    }

    /// <summary>
    /// Verifies that <see cref="DeleteCommand{TEntity}.GetLoggingContext()"/> delegates
    /// to the entity's <c>GetLoggingContext()</c>.
    /// </summary>
    [Fact]
    public void DeleteCommand_GetLoggingContext_DelegatesToEntity()
    {
        // Arrange
        var entity = TestEntityBuilder.Default().WithId("d-001").WithName("Delete Test").Build();
        var command = new DeleteCommand<IntegratoR.TestKit.Doubles.Entities.TestEntity>(entity);

        // Act
        var context = command.GetLoggingContext();

        // Assert
        context.Should().ContainKey("Id").WhoseValue.Should().Be("d-001");
        context.Should().ContainKey("Name").WhoseValue.Should().Be("Delete Test");
    }

    /// <summary>
    /// Verifies that <see cref="DeleteBatchCommand{TEntity}.GetLoggingContext()"/> returns
    /// a dictionary containing the <c>Count</c> of entities in the batch.
    /// </summary>
    [Fact]
    public void DeleteBatchCommand_GetLoggingContext_ReturnsDictionaryWithCount()
    {
        // Arrange
        var entities = new[]
        {
            TestEntityBuilder.Default().WithId("db-001").Build()
        };
        var command = new DeleteBatchCommand<IntegratoR.TestKit.Doubles.Entities.TestEntity>(entities);

        // Act
        var context = command.GetLoggingContext();

        // Assert
        context.Should().ContainKey("Count").WhoseValue.Should().Be(1);
    }
}
