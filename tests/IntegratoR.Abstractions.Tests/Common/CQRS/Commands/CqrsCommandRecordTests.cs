using System.Collections;
using FluentAssertions;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.TestKit.Builders;
using IntegratoR.TestKit.Doubles.Entities;
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

    /// <summary>
    /// Regression for the multiple-enumeration fix. The batch command ctor now takes
    /// <c>IReadOnlyList&lt;T&gt;</c> and <c>GetLoggingContext()</c> reads the O(1) <c>.Count</c> property
    /// instead of LINQ <c>Count()</c>, so it must not enumerate the sequence. This is pinned with a
    /// counting <c>IReadOnlyList&lt;T&gt;</c> wrapper that records every <c>GetEnumerator()</c> call.
    /// </summary>
    [Fact]
    public void CreateBatchCommand_GetLoggingContext_UsesCountProperty_DoesNotEnumerate()
    {
        // Arrange
        var inner = new[]
        {
            TestEntityBuilder.Default().WithId("ce-001").Build(),
            TestEntityBuilder.Default().WithId("ce-002").Build(),
            TestEntityBuilder.Default().WithId("ce-003").Build()
        };
        var counting = new CountingReadOnlyList<TestEntity>(inner);
        var command = new CreateBatchCommand<TestEntity>(counting);

        // Act — call twice to prove neither call enumerates.
        var first = command.GetLoggingContext();
        var second = command.GetLoggingContext();

        // Assert — O(1) Count returned, sequence never enumerated, same materialised instance kept.
        first.Should().ContainKey("Count").WhoseValue.Should().Be(3);
        second.Should().ContainKey("Count").WhoseValue.Should().Be(3);
        counting.GetEnumeratorCallCount.Should().Be(0);
        command.Entities.Should().BeSameAs(counting);
    }

    /// <summary>
    /// An <see cref="IReadOnlyList{T}"/> wrapper that records how many times the sequence is
    /// enumerated, used to prove <c>GetLoggingContext()</c> reads <c>.Count</c> without enumerating.
    /// </summary>
    private sealed class CountingReadOnlyList<T> : IReadOnlyList<T>
    {
        private readonly IReadOnlyList<T> _inner;

        public CountingReadOnlyList(IReadOnlyList<T> inner) => _inner = inner;

        public int GetEnumeratorCallCount { get; private set; }

        public T this[int index] => _inner[index];

        public int Count => _inner.Count;

        public IEnumerator<T> GetEnumerator()
        {
            GetEnumeratorCallCount++;
            return _inner.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
