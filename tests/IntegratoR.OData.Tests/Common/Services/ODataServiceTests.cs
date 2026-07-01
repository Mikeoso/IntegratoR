using System.Linq.Expressions;
using System.Text.Json.Serialization;
using FluentAssertions;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.Common.Services;
using IntegratoR.OData.Domain.Models;
using IntegratoR.OData.Interfaces.Services;
using IntegratoR.TestKit.Assertions;
using IntegratoR.TestKit.Builders;
using IntegratoR.TestKit.Doubles.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace IntegratoR.OData.Tests.Common.Services;

/// <summary>
/// A test entity with a <see cref="JsonIgnoreAttribute"/> decorated property to verify
/// that <see cref="ODataService{TEntity}"/> excludes such properties from the create payload.
/// </summary>
public class TestEntityWithJsonIgnore : BaseEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public required string Id { get; set; }

    /// <summary>Gets or sets the name, included in payload.</summary>
    public required string Name { get; set; }

    /// <summary>Gets or sets a property that is excluded from serialisation.</summary>
    [JsonIgnore]
    public string? ServerGeneratedField { get; set; }

    /// <inheritdoc/>
    public override object[] GetCompositeKey() => [Id];
}

/// <summary>
/// Tests for <see cref="ODataService{TEntity}"/> covering CRUD, query, batch and payload construction.
/// </summary>
public class ODataServiceTests
{
    private readonly IODataClientAdapter _client;
    private readonly ILogger<ODataService<TestEntity>> _logger;
    private readonly ODataService<TestEntity> _sut;

    /// <summary>
    /// Initialises a new instance with mocked OData client adapter and logger.
    /// </summary>
    public ODataServiceTests()
    {
        _client = Substitute.For<IODataClientAdapter>();
        _logger = Substitute.For<ILogger<ODataService<TestEntity>>>();
        _sut = new ODataService<TestEntity>(_client, _logger);
    }

    #region CRUD Tests

    /// <summary>
    /// Verifies that AddAsync inserts an entity and returns a success result.
    /// </summary>
    [Fact]
    public async Task AddAsync_ValidEntity_ReturnsSuccessResult()
    {
        // Arrange
        var entity = TestEntityBuilder.Default().Build();
        _client.CreateAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>())
            .Returns(entity);

        // Act
        var result = await _sut.AddAsync(entity, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Id.Should().Be(entity.Id);
    }

    /// <summary>
    /// Verifies that AddAsync calls CreateAsync on the adapter.
    /// </summary>
    [Fact]
    public async Task AddAsync_ValidEntity_CallsCreateAsyncOnAdapter()
    {
        // Arrange
        var entity = TestEntityBuilder.Default().Build();
        _client.CreateAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>())
            .Returns(entity);

        // Act
        await _sut.AddAsync(entity, CancellationToken.None);

        // Assert
        await _client.Received(1).CreateAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, object>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that GetByKeyAsync returns a success result with the correct entity.
    /// </summary>
    [Fact]
    public async Task GetByKeyAsync_ExistingKey_ReturnsSuccessResult()
    {
        // Arrange
        var entity = TestEntityBuilder.Default().Build();
        _client.FindByKeyAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>())
            .Returns(entity);

        // Act
        var result = await _sut.GetByKeyAsync(["test-id", "test-partition"], CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Id.Should().Be(entity.Id);
    }

    /// <summary>
    /// Verifies that GetByKeyAsync with null/empty key returns a validation error immediately.
    /// </summary>
    [Fact]
    public async Task GetByKeyAsync_NullKeyValues_ReturnsValidationError()
    {
        // Act
        var result = await _sut.GetByKeyAsync([], CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.InvalidKey");
        result.Should().HaveErrorType(ErrorType.Validation);
    }

    /// <summary>
    /// Verifies that GetByKeyAsync returns not found when entity is null.
    /// </summary>
    [Fact]
    public async Task GetByKeyAsync_EntityNotFound_ReturnsNotFoundError()
    {
        // Arrange
        _client.FindByKeyAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>())
            .Returns((TestEntity?)null);

        // Act
        var result = await _sut.GetByKeyAsync(["missing-id", "missing-partition"], CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.NotFound");
    }

    /// <summary>
    /// Verifies that UpdateAsync with a valid entity returns a success result.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ValidEntity_ReturnsSuccessResult()
    {
        // Arrange
        var entity = TestEntityBuilder.Default().Build();
        _client.UpdateAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>())
            .Returns(entity);

        // Act
        var result = await _sut.UpdateAsync(entity, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
    }

    /// <summary>
    /// Regression: a composite-key PATCH that returns 204 No Content makes the adapter yield null.
    /// The update still succeeded, so UpdateAsync must return the caller's entity rather than a
    /// successful Result carrying a null Value (which previously NRE'd consumers reading it).
    /// </summary>
    [Fact]
    public async Task UpdateAsync_AdapterReturnsNull_ReturnsSuccessWithInputEntity()
    {
        // Arrange — the adapter returns null, mimicking a 204 No Content response with no body.
        var entity = TestEntityBuilder.Default().Build();
        _client.UpdateAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>())
            .Returns((TestEntity)null!);

        // Act
        var result = await _sut.UpdateAsync(entity, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().BeSameAs(entity);
    }

    /// <summary>
    /// Verifies that UpdateAsync with a null entity returns a validation error immediately.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_NullEntity_ReturnsValidationError()
    {
        // Act
        var result = await _sut.UpdateAsync(null!, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("Validation.NullEntity");
        result.Should().HaveErrorType(ErrorType.Validation);
    }

    /// <summary>
    /// Verifies that DeleteAsync with a valid entity returns a success result.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ValidEntity_ReturnsSuccessResult()
    {
        // Arrange
        var entity = TestEntityBuilder.Default().Build();
        _client.DeleteAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.DeleteAsync(entity, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
    }

    /// <summary>
    /// Verifies that DeleteAsync with a null entity returns a validation error immediately.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_NullEntity_ReturnsValidationError()
    {
        // Act
        var result = await _sut.DeleteAsync(null!, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("Validation.NullEntity");
    }

    #endregion

    #region Query / Find / Count Tests

    /// <summary>
    /// Verifies that FindAsync without filter returns all entities.
    /// </summary>
    [Fact]
    public async Task FindAsync_NoFilter_ReturnsAllEntities()
    {
        // Arrange
        var entities = new List<TestEntity> { TestEntityBuilder.Default().Build() };
        _client.FindEntriesAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Any<Expression<Func<TestEntity, bool>>?>(),
            Arg.Any<Expression<Func<TestEntity, object>>?>(),
            Arg.Any<Expression<Func<TestEntity, object>>?>(),
            Arg.Any<IReadOnlyList<(Expression<Func<TestEntity, object>> KeySelector, bool Descending)>?>(),
            Arg.Any<int?>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>())
            .Returns(entities);

        // Act
        var result = await _sut.FindAsync(null, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().HaveCount(1);
    }

    /// <summary>
    /// Verifies that FindAsync with a filter expression passes the filter to the adapter.
    /// </summary>
    [Fact]
    public async Task FindAsync_WithFilter_AppliesFilterAndReturnsEntities()
    {
        // Arrange
        var entity = TestEntityBuilder.Default().WithName("Filtered").Build();
        var filtered = new List<TestEntity> { entity };
        _client.FindEntriesAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Any<Expression<Func<TestEntity, bool>>?>(),
            Arg.Any<Expression<Func<TestEntity, object>>?>(),
            Arg.Any<Expression<Func<TestEntity, object>>?>(),
            Arg.Any<IReadOnlyList<(Expression<Func<TestEntity, object>> KeySelector, bool Descending)>?>(),
            Arg.Any<int?>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>())
            .Returns(filtered);

        // Act
        var result = await _sut.FindAsync(e => e.Name == "Filtered", CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().HaveCount(1);
    }

    /// <summary>
    /// Verifies that QueryAsync applies skip and top parameters to the adapter call.
    /// </summary>
    [Fact]
    public async Task QueryAsync_WithSkipAndTop_AppliesSkipAndTop()
    {
        // Arrange
        _client.FindEntriesAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Any<Expression<Func<TestEntity, bool>>?>(),
            Arg.Any<Expression<Func<TestEntity, object>>?>(),
            Arg.Any<Expression<Func<TestEntity, object>>?>(),
            Arg.Any<IReadOnlyList<(Expression<Func<TestEntity, object>> KeySelector, bool Descending)>?>(),
            Arg.Any<int?>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<TestEntity>());

        // Act
        IReadOnlyList<(Expression<Func<TestEntity, object>> KeySelector, bool Descending)>? noOrderBy = null;
        var result = await _sut.QueryAsync(filter: null, orderBy: noOrderBy, skip: 10, top: 5, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        await _client.Received(1).FindEntriesAsync<TestEntity>(
            Arg.Any<string>(),
            null,
            null,
            null,
            null,
            10,
            5,
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that FindAllAsync returns a success result with all entities.
    /// </summary>
    [Fact]
    public async Task FindAllAsync_ReturnsSuccessWithAllEntities()
    {
        // Arrange
        var entities = new[] { TestEntityBuilder.Default().Build(), TestEntityBuilder.Default().Build() };
        _client.FindEntriesAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Any<Expression<Func<TestEntity, bool>>?>(),
            Arg.Any<Expression<Func<TestEntity, object>>?>(),
            Arg.Any<Expression<Func<TestEntity, object>>?>(),
            Arg.Any<IReadOnlyList<(Expression<Func<TestEntity, object>> KeySelector, bool Descending)>?>(),
            Arg.Any<int?>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>())
            .Returns(entities);

        // Act
        var result = await _sut.FindAllAsync(CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().HaveCount(2);
    }

    /// <summary>
    /// Verifies that the obsolete <c>FindAll</c> still works (delegating to <c>FindAllAsync</c>).
    /// </summary>
    [Fact]
    public async Task FindAll_Obsolete_StillReturnsEntities()
    {
        // Arrange
        var entities = new[] { TestEntityBuilder.Default().Build(), TestEntityBuilder.Default().Build() };
        _client.FindEntriesAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Any<Expression<Func<TestEntity, bool>>?>(),
            Arg.Any<Expression<Func<TestEntity, object>>?>(),
            Arg.Any<Expression<Func<TestEntity, object>>?>(),
            Arg.Any<IReadOnlyList<(Expression<Func<TestEntity, object>> KeySelector, bool Descending)>?>(),
            Arg.Any<int?>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>())
            .Returns(entities);

        // Act
#pragma warning disable CS0618 // Type or member is obsolete — proving the obsolete overload still works
        var result = await _sut.FindAll(CancellationToken.None);
#pragma warning restore CS0618

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().HaveCount(2);
    }

    /// <summary>
    /// Verifies that CountAsync returns a success result with the scalar count value.
    /// </summary>
    [Fact]
    public async Task CountAsync_NoFilter_ReturnsSuccessWithCount()
    {
        // Arrange
        _client.CountAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Any<Expression<Func<TestEntity, bool>>?>(),
            Arg.Any<CancellationToken>())
            .Returns(42);

        // Act
        var result = await _sut.CountAsync(cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().Be(42);
    }

    /// <summary>
    /// Verifies that QueryAsync without any parameters returns all entities.
    /// </summary>
    [Fact]
    public async Task QueryAsync_NoParameters_ReturnsAllEntities()
    {
        // Arrange
        var entities = new[] { TestEntityBuilder.Default().Build() };
        _client.FindEntriesAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Any<Expression<Func<TestEntity, bool>>?>(),
            Arg.Any<Expression<Func<TestEntity, object>>?>(),
            Arg.Any<Expression<Func<TestEntity, object>>?>(),
            Arg.Any<IReadOnlyList<(Expression<Func<TestEntity, object>> KeySelector, bool Descending)>?>(),
            Arg.Any<int?>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>())
            .Returns(entities);

        // Act
        IReadOnlyList<(Expression<Func<TestEntity, object>> KeySelector, bool Descending)>? noOrderBy = null;
        var result = await _sut.QueryAsync(filter: null, orderBy: noOrderBy, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
    }

    /// <summary>
    /// Verifies that the new strongly-typed QueryAsync overload forwards its orderBy argument to
    /// the adapter's FindEntriesAsync — the wiring the old Func-based overload silently dropped.
    /// </summary>
    [Fact]
    public async Task QueryAsync_WithOrderBy_ForwardsOrderByToAdapter()
    {
        // Arrange
        IReadOnlyList<(Expression<Func<TestEntity, object>> KeySelector, bool Descending)>? capturedOrderBy = null;
        _client.FindEntriesAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Any<Expression<Func<TestEntity, bool>>?>(),
            Arg.Any<Expression<Func<TestEntity, object>>?>(),
            Arg.Any<Expression<Func<TestEntity, object>>?>(),
            Arg.Do<IReadOnlyList<(Expression<Func<TestEntity, object>> KeySelector, bool Descending)>?>(o => capturedOrderBy = o),
            Arg.Any<int?>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<TestEntity>());

        var orderBy = new (Expression<Func<TestEntity, object>> KeySelector, bool Descending)[]
        {
            (e => e.Id, false),
            (e => e.Name, true)
        };

        // Act
        var result = await _sut.QueryAsync(filter: null, orderBy: orderBy, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        capturedOrderBy.Should().NotBeNull();
        capturedOrderBy.Should().BeEquivalentTo(orderBy);
    }

    /// <summary>
    /// Verifies that the [Obsolete] Func-based QueryAsync overload still compiles and returns
    /// success (its body is retained as a no-op for orderBy). CS0618 is suppressed narrowly here
    /// because the test intentionally exercises the deprecated overload.
    /// </summary>
    [Fact]
    public async Task QueryAsync_ObsoleteFuncOverload_StillCompilesAndReturnsSuccess()
    {
        // Arrange
        _client.FindEntriesAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Any<Expression<Func<TestEntity, bool>>?>(),
            Arg.Any<Expression<Func<TestEntity, object>>?>(),
            Arg.Any<Expression<Func<TestEntity, object>>?>(),
            Arg.Any<IReadOnlyList<(Expression<Func<TestEntity, object>> KeySelector, bool Descending)>?>(),
            Arg.Any<int?>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<TestEntity>());

        // Act
#pragma warning disable CS0618 // intentionally exercising the obsolete Func-based overload
        var result = await _sut.QueryAsync(
            filter: null,
            orderBy: (Func<IQueryable<TestEntity>, IOrderedQueryable<TestEntity>>?)null,
            cancellationToken: CancellationToken.None);
#pragma warning restore CS0618

        // Assert
        result.Should().BeSuccessful();
    }

    #endregion

    #region Batch Tests

    /// <summary>
    /// Verifies that AddBatchAsync wraps adapter failures into a failed result.
    /// </summary>
    [Fact]
    public async Task AddBatchAsync_WhenClientThrows_ReturnsFailedResult()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            TestEntityBuilder.Default().WithId("batch-1").Build()
        };

        _client.When(x => x.BatchCreateAsync(
            Arg.Any<string>(),
            Arg.Any<IEnumerable<IDictionary<string, object>>>(),
            Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("batch failed"));

        // Act
        var result = await _sut.AddBatchAsync(entities, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.UnexpectedError");
    }

    /// <summary>
    /// Verifies that DeleteBatchAsync wraps adapter failures into a failed result.
    /// </summary>
    [Fact]
    public async Task DeleteBatchAsync_WhenClientThrows_ReturnsFailedResult()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            TestEntityBuilder.Default().WithId("batch-1").Build()
        };

        _client.When(x => x.BatchDeleteAsync(
            Arg.Any<string>(),
            Arg.Any<IEnumerable<object>>(),
            Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("batch failed"));

        // Act
        var result = await _sut.DeleteBatchAsync(entities, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.UnexpectedError");
    }

    /// <summary>
    /// Verifies that UpdateBatchAsync wraps adapter failures into a failed result.
    /// </summary>
    [Fact]
    public async Task UpdateBatchAsync_WhenClientThrows_ReturnsFailedResult()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            TestEntityBuilder.Default().WithId("batch-1").Build()
        };

        _client.When(x => x.BatchUpdateAsync(
            Arg.Any<string>(),
            Arg.Any<IEnumerable<(object Key, IDictionary<string, object> Payload)>>(),
            Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("batch failed"));

        // Act
        var result = await _sut.UpdateBatchAsync(entities, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.UnexpectedError");
    }

    /// <summary>
    /// Verifies that AddBatchAsync returns success when the adapter succeeds.
    /// </summary>
    [Fact]
    public async Task AddBatchAsync_WhenClientSucceeds_ReturnsSuccessResult()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            TestEntityBuilder.Default().WithId("batch-1").Build()
        };

        _client.BatchCreateAsync(
            Arg.Any<string>(),
            Arg.Any<IEnumerable<IDictionary<string, object>>>(),
            Arg.Any<CancellationToken>())
            .Returns(SuccessfulBatchResults(1));

        // Act
        var result = await _sut.AddBatchAsync(entities, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
    }

    #endregion

    #region CreatePayload Attribute Tests

    /// <summary>
    /// Verifies that CreatePayload includes only non-null and non-default properties in the payload.
    /// </summary>
    [Fact]
    public async Task AddAsync_EntityWithNullProperty_ExcludesNullFromPayload()
    {
        // Arrange - entity with null Description (optional)
        var entity = new TestEntity { Id = "id1", PartitionKey = "pk1", Name = "Name", Description = null };

        IDictionary<string, object>? capturedPayload = null;
        _client.CreateAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Do<object>(p => capturedPayload = p as IDictionary<string, object>),
            Arg.Any<CancellationToken>())
            .Returns(entity);

        // Act
        await _sut.AddAsync(entity, CancellationToken.None);

        // Assert
        capturedPayload.Should().NotBeNull();
        capturedPayload!.Should().NotContainKey("Description");
    }

    /// <summary>
    /// Verifies that CreatePayload for TestEntityWithODataAttributes excludes IgnoreOnCreate fields on create.
    /// </summary>
    [Fact]
    public async Task AddAsync_EntityWithIgnoreOnCreateAttribute_ExcludesFieldFromPayload()
    {
        // Arrange
        var odataSut = new ODataService<TestEntityWithODataAttributes>(_client, Substitute.For<ILogger<ODataService<TestEntityWithODataAttributes>>>());

        IDictionary<string, object>? capturedPayload = null;
        _client.CreateAsync<TestEntityWithODataAttributes>(
            Arg.Any<string>(),
            Arg.Do<object>(p => capturedPayload = p as IDictionary<string, object>),
            Arg.Any<CancellationToken>())
            .Returns(new TestEntityWithODataAttributes { Id = "generated-id", Name = "Name", ReadOnlyField = "readonly" });

        var entity = new TestEntityWithODataAttributes { Id = "client-id", Name = "Name", ReadOnlyField = "readonly" };

        // Act
        await odataSut.AddAsync(entity, CancellationToken.None);

        // Assert - Id has [ODataField(IgnoreOnCreate = true)] so should NOT be in payload
        capturedPayload.Should().NotBeNull();
        capturedPayload!.Should().NotContainKey("Id");
        capturedPayload.Should().ContainKey("Name");
    }

    /// <summary>
    /// Verifies that AddAsync for TestEntityWithODataAttributes excludes IgnoreOnCreate fields and includes
    /// IgnoreOnUpdate fields in the create payload (since CreatePayload is only used in AddAsync).
    /// </summary>
    [Fact]
    public async Task AddAsync_EntityWithIgnoreOnUpdateAttribute_IncludesFieldInCreatePayload()
    {
        // Arrange -- ReadOnlyField has [ODataField(IgnoreOnUpdate = true)] but should be INCLUDED on create
        var odataSut = new ODataService<TestEntityWithODataAttributes>(_client, Substitute.For<ILogger<ODataService<TestEntityWithODataAttributes>>>());

        IDictionary<string, object>? capturedPayload = null;
        _client.CreateAsync<TestEntityWithODataAttributes>(
            Arg.Any<string>(),
            Arg.Do<object>(p => capturedPayload = p as IDictionary<string, object>),
            Arg.Any<CancellationToken>())
            .Returns(new TestEntityWithODataAttributes { Id = "generated-id", Name = "Name", ReadOnlyField = "readonly" });

        var entity = new TestEntityWithODataAttributes { Id = "client-id", Name = "Name", ReadOnlyField = "readonly-value" };

        // Act
        await odataSut.AddAsync(entity, CancellationToken.None);

        // Assert - ReadOnlyField should be included in create payload (only IgnoreOnCreate fields are excluded)
        capturedPayload.Should().NotBeNull();
        capturedPayload!.Should().ContainKey("ReadOnlyField");
        capturedPayload.Should().NotContainKey("Id"); // [ODataField(IgnoreOnCreate = true)]
    }

    /// <summary>
    /// Verifies that CreatePayload includes properties with non-default values in the payload.
    /// </summary>
    [Fact]
    public async Task AddAsync_EntityWithAllPropertiesSet_IncludesAllInPayload()
    {
        // Arrange
        var entity = new TestEntity
        {
            Id = "id1",
            PartitionKey = "pk1",
            Name = "Test Name",
            Description = "Test Description"
        };

        IDictionary<string, object>? capturedPayload = null;
        _client.CreateAsync<TestEntity>(
            Arg.Any<string>(),
            Arg.Do<object>(p => capturedPayload = p as IDictionary<string, object>),
            Arg.Any<CancellationToken>())
            .Returns(entity);

        // Act
        await _sut.AddAsync(entity, CancellationToken.None);

        // Assert
        capturedPayload.Should().NotBeNull();
        capturedPayload!.Should().ContainKey("Name");
        capturedPayload.Should().ContainKey("Description");
    }

    /// <summary>
    /// Verifies that CreatePayload excludes properties decorated with <see cref="JsonIgnoreAttribute"/>.
    /// </summary>
    [Fact]
    public async Task AddAsync_EntityWithJsonIgnoreAttribute_ExcludesFieldFromPayload()
    {
        // Arrange
        var jsonIgnoreSut = new ODataService<TestEntityWithJsonIgnore>(
            _client,
            Substitute.For<ILogger<ODataService<TestEntityWithJsonIgnore>>>());

        IDictionary<string, object>? capturedPayload = null;
        _client.CreateAsync<TestEntityWithJsonIgnore>(
            Arg.Any<string>(),
            Arg.Do<object>(p => capturedPayload = p as IDictionary<string, object>),
            Arg.Any<CancellationToken>())
            .Returns(new TestEntityWithJsonIgnore { Id = "id1", Name = "Name", ServerGeneratedField = "server" });

        var entity = new TestEntityWithJsonIgnore
        {
            Id = "id1",
            Name = "Test Name",
            ServerGeneratedField = "should-be-excluded"
        };

        // Act
        await jsonIgnoreSut.AddAsync(entity, CancellationToken.None);

        // Assert - ServerGeneratedField has [JsonIgnore] so should NOT appear in the payload
        capturedPayload.Should().NotBeNull();
        capturedPayload!.Should().ContainKey("Name");
        capturedPayload.Should().NotContainKey("ServerGeneratedField");
    }

    /// <summary>
    /// Verifies that UpdateAsync routes through CreatePayload and excludes IgnoreOnUpdate fields.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_EntityWithIgnoreOnUpdateAttribute_ExcludesFieldFromPayload()
    {
        // Arrange
        var odataSut = new ODataService<TestEntityWithODataAttributes>(_client, Substitute.For<ILogger<ODataService<TestEntityWithODataAttributes>>>());

        IDictionary<string, object>? capturedPayload = null;
        _client.UpdateAsync<TestEntityWithODataAttributes>(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Do<object>(p => capturedPayload = p as IDictionary<string, object>),
            Arg.Any<CancellationToken>())
            .Returns(new TestEntityWithODataAttributes { Id = "id1", Name = "Name", ReadOnlyField = "readonly" });

        var entity = new TestEntityWithODataAttributes { Id = "id1", Name = "Updated Name", ReadOnlyField = "should-be-excluded" };

        // Act
        await odataSut.UpdateAsync(entity, CancellationToken.None);

        // Assert - ReadOnlyField has [ODataField(IgnoreOnUpdate = true)] so should NOT be in payload
        capturedPayload.Should().NotBeNull();
        capturedPayload!.Should().NotContainKey("ReadOnlyField");
        capturedPayload.Should().ContainKey("Name");
    }

    /// <summary>
    /// Verifies that UpdateAsync includes IgnoreOnCreate fields in update payloads (they're only excluded on create).
    /// </summary>
    [Fact]
    public async Task UpdateAsync_EntityWithIgnoreOnCreateAttribute_IncludesFieldInUpdatePayload()
    {
        // Arrange
        var odataSut = new ODataService<TestEntityWithODataAttributes>(_client, Substitute.For<ILogger<ODataService<TestEntityWithODataAttributes>>>());

        IDictionary<string, object>? capturedPayload = null;
        _client.UpdateAsync<TestEntityWithODataAttributes>(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Do<object>(p => capturedPayload = p as IDictionary<string, object>),
            Arg.Any<CancellationToken>())
            .Returns(new TestEntityWithODataAttributes { Id = "id1", Name = "Name", ReadOnlyField = "readonly" });

        var entity = new TestEntityWithODataAttributes { Id = "server-id", Name = "Name", ReadOnlyField = "readonly" };

        // Act
        await odataSut.UpdateAsync(entity, CancellationToken.None);

        // Assert - Id has [ODataField(IgnoreOnCreate = true)] but should be INCLUDED in update payload
        capturedPayload.Should().NotBeNull();
        capturedPayload!.Should().ContainKey("Id");
    }

    /// <summary>
    /// Verifies that AddBatchAsync routes through CreatePayload and excludes IgnoreOnCreate fields.
    /// </summary>
    [Fact]
    public async Task AddBatchAsync_EntityWithIgnoreOnCreateAttribute_ExcludesFieldFromPayloads()
    {
        // Arrange
        var odataSut = new ODataService<TestEntityWithODataAttributes>(_client, Substitute.For<ILogger<ODataService<TestEntityWithODataAttributes>>>());

        IEnumerable<IDictionary<string, object>>? capturedPayloads = null;
        _client.BatchCreateAsync(
            Arg.Any<string>(),
            Arg.Do<IEnumerable<IDictionary<string, object>>>(p => capturedPayloads = p),
            Arg.Any<CancellationToken>())
            .Returns(callInfo => SuccessfulBatchResults(capturedPayloads?.Count() ?? 0));

        var entities = new List<TestEntityWithODataAttributes>
        {
            new() { Id = "batch-1", Name = "Entity 1", ReadOnlyField = "readonly1" },
            new() { Id = "batch-2", Name = "Entity 2", ReadOnlyField = "readonly2" }
        };

        // Act
        await odataSut.AddBatchAsync(entities, CancellationToken.None);

        // Assert - Id has [ODataField(IgnoreOnCreate = true)] so should NOT appear in batch payloads
        capturedPayloads.Should().NotBeNull();
        var payloadList = capturedPayloads!.ToList();
        payloadList.Should().HaveCount(2);
        payloadList[0].Should().NotContainKey("Id");
        payloadList[0].Should().ContainKey("Name");
        payloadList[0].Should().ContainKey("ReadOnlyField"); // IgnoreOnUpdate only, included on create
    }

    /// <summary>
    /// Verifies that UpdateBatchAsync routes through CreatePayload and excludes IgnoreOnUpdate fields.
    /// </summary>
    [Fact]
    public async Task UpdateBatchAsync_EntityWithIgnoreOnUpdateAttribute_ExcludesFieldFromPayloads()
    {
        // Arrange
        var odataSut = new ODataService<TestEntityWithODataAttributes>(_client, Substitute.For<ILogger<ODataService<TestEntityWithODataAttributes>>>());

        IEnumerable<(object Key, IDictionary<string, object> Payload)>? capturedItems = null;
        _client.BatchUpdateAsync(
            Arg.Any<string>(),
            Arg.Do<IEnumerable<(object Key, IDictionary<string, object> Payload)>>(p => capturedItems = p),
            Arg.Any<CancellationToken>())
            .Returns(callInfo => SuccessfulBatchResults(capturedItems?.Count() ?? 0));

        var entities = new List<TestEntityWithODataAttributes>
        {
            new() { Id = "batch-1", Name = "Entity 1", ReadOnlyField = "readonly1" },
            new() { Id = "batch-2", Name = "Entity 2", ReadOnlyField = "readonly2" }
        };

        // Act
        await odataSut.UpdateBatchAsync(entities, CancellationToken.None);

        // Assert - ReadOnlyField has [ODataField(IgnoreOnUpdate = true)] so should NOT appear
        capturedItems.Should().NotBeNull();
        var itemList = capturedItems!.ToList();
        itemList.Should().HaveCount(2);
        itemList[0].Payload.Should().NotContainKey("ReadOnlyField");
        itemList[0].Payload.Should().ContainKey("Name");
        itemList[0].Payload.Should().ContainKey("Id"); // IgnoreOnCreate only, included on update
    }

    /// <summary>
    /// Verifies that AddBatchAsync surfaces per-entity errors when some operations fail.
    /// </summary>
    [Fact]
    public async Task AddBatchAsync_WithPartialFailure_ReturnsBatchFailedWithPerEntityErrors()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            TestEntityBuilder.Default().WithId("ok-1").Build(),
            TestEntityBuilder.Default().WithId("fail-1").Build()
        };

        IReadOnlyList<BatchOperationResult> mixedResults = new List<BatchOperationResult>
        {
            new() { Index = 0, StatusCode = 201, IsSuccess = true },
            new() { Index = 1, StatusCode = 400, IsSuccess = false, ErrorMessage = "Validation failed for entity",
                ResponseBody = """{"error":{"message":"Bad request","innererror":{"message":"Field 'Amount' is required"}}}""" }
        };

        _client.BatchCreateAsync(
            Arg.Any<string>(),
            Arg.Any<IEnumerable<IDictionary<string, object>>>(),
            Arg.Any<CancellationToken>())
            .Returns(mixedResults);

        // Act
        var result = await _sut.AddBatchAsync(entities, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.BatchFailed");
        // Should also contain per-entity error
        result.Errors.Should().Contain(e => e.Message.Contains("Field 'Amount' is required"));
    }

    #endregion

    #region D365 Attribute Tests (AllowEdit, AllowEditOnCreate, IsRequired)

    /// <summary>
    /// Verifies that AllowEdit=false excludes a property from update payloads.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_EntityWithAllowEditFalse_ExcludesFieldFromPayload()
    {
        // Arrange
        var odataSut = new ODataService<TestEntityWithD365Attributes>(_client, Substitute.For<ILogger<ODataService<TestEntityWithD365Attributes>>>());

        IDictionary<string, object>? capturedPayload = null;
        _client.UpdateAsync<TestEntityWithD365Attributes>(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Do<object>(p => capturedPayload = p as IDictionary<string, object>),
            Arg.Any<CancellationToken>())
            .Returns(new TestEntityWithD365Attributes { DataAreaId = "USMF", Amount = 100m });

        var entity = new TestEntityWithD365Attributes { DataAreaId = "USMF", JournalBatchNumber = "JN-001", Description = "Test", Amount = 100m };

        // Act
        await odataSut.UpdateAsync(entity, CancellationToken.None);

        // Assert - DataAreaId has AllowEdit=false, JournalBatchNumber has AllowEdit=false
        capturedPayload.Should().NotBeNull();
        capturedPayload!.Should().NotContainKey("DataAreaId");
        capturedPayload.Should().NotContainKey("JournalBatchNumber");
        capturedPayload.Should().ContainKey("Description");
        capturedPayload.Should().ContainKey("Amount");
    }

    /// <summary>
    /// Verifies that AllowEditOnCreate=false excludes a property from create payloads.
    /// </summary>
    [Fact]
    public async Task AddAsync_EntityWithAllowEditOnCreateFalse_ExcludesFieldFromPayload()
    {
        // Arrange
        var odataSut = new ODataService<TestEntityWithD365Attributes>(_client, Substitute.For<ILogger<ODataService<TestEntityWithD365Attributes>>>());

        IDictionary<string, object>? capturedPayload = null;
        _client.CreateAsync<TestEntityWithD365Attributes>(
            Arg.Any<string>(),
            Arg.Do<object>(p => capturedPayload = p as IDictionary<string, object>),
            Arg.Any<CancellationToken>())
            .Returns(new TestEntityWithD365Attributes { DataAreaId = "USMF", Amount = 100m, JournalName = "GJ" });

        var entity = new TestEntityWithD365Attributes { DataAreaId = "USMF", JournalBatchNumber = "JN-001", Description = "Test", Amount = 100m, JournalName = "GJ" };

        // Act
        await odataSut.AddAsync(entity, CancellationToken.None);

        // Assert - JournalBatchNumber has AllowEditOnCreate=false so excluded from create
        capturedPayload.Should().NotBeNull();
        capturedPayload!.Should().NotContainKey("JournalBatchNumber");
        // DataAreaId has AllowEdit=false but AllowEditOnCreate defaults to true, so included on create
        capturedPayload.Should().ContainKey("DataAreaId");
        capturedPayload.Should().ContainKey("Amount");
    }

    /// <summary>
    /// Verifies that IsRequired=true with a null value on create returns a validation error.
    /// </summary>
    [Fact]
    public async Task AddAsync_EntityWithMissingRequiredField_ReturnsValidationError()
    {
        // Arrange
        var odataSut = new ODataService<TestEntityWithD365Attributes>(_client, Substitute.For<ILogger<ODataService<TestEntityWithD365Attributes>>>());

        // JournalName is required but left null
        var entity = new TestEntityWithD365Attributes { DataAreaId = "USMF", Amount = 100m, JournalName = null };

        // Act
        var result = await odataSut.AddAsync(entity, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntityWithD365Attributes.RequiredFieldMissing");
        result.Should().HaveErrorType(ErrorType.Validation);
    }

    /// <summary>
    /// Verifies that required value-type fields with default value (0m) are included in the payload, not rejected.
    /// </summary>
    [Fact]
    public async Task AddAsync_EntityWithRequiredFieldAtDefaultValue_IncludesInPayload()
    {
        // Arrange
        var odataSut = new ODataService<TestEntityWithD365Attributes>(_client, Substitute.For<ILogger<ODataService<TestEntityWithD365Attributes>>>());

        IDictionary<string, object>? capturedPayload = null;
        _client.CreateAsync<TestEntityWithD365Attributes>(
            Arg.Any<string>(),
            Arg.Do<object>(p => capturedPayload = p as IDictionary<string, object>),
            Arg.Any<CancellationToken>())
            .Returns(new TestEntityWithD365Attributes { DataAreaId = "USMF", Amount = 0m, JournalName = "GJ" });

        var entity = new TestEntityWithD365Attributes { DataAreaId = "USMF", Amount = 0m, JournalName = "GJ" };

        // Act
        await odataSut.AddAsync(entity, CancellationToken.None);

        // Assert — Amount is 0m (default) but IsRequired=true, so it must be included
        capturedPayload.Should().NotBeNull();
        capturedPayload!.Should().ContainKey("Amount");
    }

    /// <summary>
    /// Verifies backward compatibility: IgnoreOnCreate still works for hand-written entities.
    /// </summary>
    [Fact]
    public async Task AddAsync_EntityWithLegacyIgnoreOnCreate_StillExcludesField()
    {
        // Arrange — uses TestEntityWithODataAttributes which has IgnoreOnCreate/IgnoreOnUpdate
        var odataSut = new ODataService<TestEntityWithODataAttributes>(_client, Substitute.For<ILogger<ODataService<TestEntityWithODataAttributes>>>());

        IDictionary<string, object>? capturedPayload = null;
        _client.CreateAsync<TestEntityWithODataAttributes>(
            Arg.Any<string>(),
            Arg.Do<object>(p => capturedPayload = p as IDictionary<string, object>),
            Arg.Any<CancellationToken>())
            .Returns(new TestEntityWithODataAttributes { Id = "gen", Name = "Name", ReadOnlyField = "ro" });

        var entity = new TestEntityWithODataAttributes { Id = "client-id", Name = "Name", ReadOnlyField = "readonly" };

        // Act
        await odataSut.AddAsync(entity, CancellationToken.None);

        // Assert - legacy IgnoreOnCreate still works
        capturedPayload.Should().NotBeNull();
        capturedPayload!.Should().NotContainKey("Id");
        capturedPayload.Should().ContainKey("Name");
        capturedPayload.Should().ContainKey("ReadOnlyField");
    }

    /// <summary>
    /// Verifies that IsRequired validation only fires on create, not on update.
    /// A null required field on UpdateAsync must not return a validation error.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_EntityWithNullRequiredField_DoesNotValidateIsRequired()
    {
        // Arrange — JournalName is [ODataField(IsRequired = true)] but left null
        var odataSut = new ODataService<TestEntityWithD365Attributes>(_client, Substitute.For<ILogger<ODataService<TestEntityWithD365Attributes>>>());

        _client.UpdateAsync<TestEntityWithD365Attributes>(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>())
            .Returns(new TestEntityWithD365Attributes { DataAreaId = "USMF", Amount = 100m });

        // JournalBatchNumber is set so the composite key is valid; the test isolates IsRequired,
        // not key validation.
        var entity = new TestEntityWithD365Attributes { DataAreaId = "USMF", JournalBatchNumber = "JN-001", Amount = 100m, JournalName = null };

        // Act
        var result = await odataSut.UpdateAsync(entity, CancellationToken.None);

        // Assert — IsRequired validation must NOT fire on update; the call should succeed
        result.Should().BeSuccessful();
    }

    #endregion

    #region Helpers

    private static IReadOnlyList<BatchOperationResult> SuccessfulBatchResults(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new BatchOperationResult
            {
                Index = i,
                StatusCode = 200,
                IsSuccess = true
            })
            .ToList();
    }

    #endregion
}
