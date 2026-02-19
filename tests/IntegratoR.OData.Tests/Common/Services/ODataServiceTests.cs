using System.Text.Json.Serialization;
using FluentAssertions;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.Common.Services;
using IntegratoR.TestKit.Assertions;
using IntegratoR.TestKit.Builders;
using IntegratoR.TestKit.Doubles.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Simple.OData.Client;
using Xunit;

namespace IntegratoR.OData.Tests.Common.Services;

/// <summary>
/// A test entity with a <see cref="JsonIgnoreAttribute"/> decorated property to verify
/// that <see cref="ODataService{TEntity}"/> excludes such properties from the create payload.
/// </summary>
public class TestEntityWithJsonIgnore : BaseEntity<string>
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
    private readonly IODataClient _client;
    private readonly ILogger<ODataService<TestEntity>> _logger;
    private readonly ODataService<TestEntity> _sut;
    private readonly IBoundClient<TestEntity> _boundClient;

    /// <summary>
    /// Initialises a new instance with mocked OData client and logger.
    /// </summary>
    public ODataServiceTests()
    {
        _client = Substitute.For<IODataClient>();
        _logger = Substitute.For<ILogger<ODataService<TestEntity>>>();
        _boundClient = Substitute.For<IBoundClient<TestEntity>>();

        // Set up full fluent chain
        _client.For<TestEntity>(null).Returns(_boundClient);
        _boundClient.Key(Arg.Any<object[]>()).Returns(_boundClient);
        _boundClient.Key(Arg.Any<object>()).Returns(_boundClient);
        _boundClient.Set(Arg.Any<IDictionary<string, object>>()).Returns(_boundClient);
        _boundClient.Set(Arg.Any<object>()).Returns(_boundClient);
        _boundClient.Filter(Arg.Any<System.Linq.Expressions.Expression<Func<TestEntity, bool>>>()).Returns(_boundClient);
        _boundClient.Expand(Arg.Any<System.Linq.Expressions.Expression<Func<TestEntity, object>>>()).Returns(_boundClient);
        _boundClient.Select(Arg.Any<System.Linq.Expressions.Expression<Func<TestEntity, object>>>()).Returns(_boundClient);
        _boundClient.Skip(Arg.Any<long>()).Returns(_boundClient);
        _boundClient.Top(Arg.Any<long>()).Returns(_boundClient);
        _boundClient.Count().Returns(_boundClient);

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
        _boundClient.InsertEntryAsync(true, Arg.Any<CancellationToken>()).Returns(entity);

        // Act
        var result = await _sut.AddAsync(entity, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Id.Should().Be(entity.Id);
    }

    /// <summary>
    /// Verifies that AddAsync calls InsertEntryAsync with the result flag set to true.
    /// </summary>
    [Fact]
    public async Task AddAsync_ValidEntity_CallsInsertEntryAsyncWithResultFlag()
    {
        // Arrange
        var entity = TestEntityBuilder.Default().Build();
        _boundClient.InsertEntryAsync(true, Arg.Any<CancellationToken>()).Returns(entity);

        // Act
        await _sut.AddAsync(entity, CancellationToken.None);

        // Assert
        await _boundClient.Received(1).InsertEntryAsync(true, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that GetByKeyAsync returns a success result with the correct entity.
    /// </summary>
    [Fact]
    public async Task GetByKeyAsync_ExistingKey_ReturnsSuccessResult()
    {
        // Arrange
        var entity = TestEntityBuilder.Default().Build();
        _boundClient.FindEntryAsync(Arg.Any<CancellationToken>()).Returns(entity);

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
    /// Verifies that GetByKeyAsync throws ODataNotFoundException when entity is null (mapped as not found).
    /// </summary>
    [Fact]
    public async Task GetByKeyAsync_EntityNotFound_ReturnsNotFoundError()
    {
        // Arrange
        _boundClient.FindEntryAsync(Arg.Any<CancellationToken>()).Returns((TestEntity)null!);

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
        _boundClient.UpdateEntryAsync(Arg.Any<CancellationToken>()).Returns(entity);

        // Act
        var result = await _sut.UpdateAsync(entity, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
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
        _boundClient.DeleteEntryAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

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
        _boundClient.FindEntriesAsync(Arg.Any<CancellationToken>()).Returns(entities);

        // Act
        var result = await _sut.FindAsync(null, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().HaveCount(1);
    }

    /// <summary>
    /// Verifies that FindAsync with a filter expression passes the filter to the OData query.
    /// </summary>
    [Fact]
    public async Task FindAsync_WithFilter_AppliesFilterAndReturnsEntities()
    {
        // Arrange
        var entity = TestEntityBuilder.Default().WithName("Filtered").Build();
        var filtered = new List<TestEntity> { entity };
        _boundClient.FindEntriesAsync(Arg.Any<CancellationToken>()).Returns(filtered);

        // Act
        var result = await _sut.FindAsync(e => e.Name == "Filtered", CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().HaveCount(1);
        _boundClient.Received(1).Filter(Arg.Any<System.Linq.Expressions.Expression<Func<TestEntity, bool>>>());
    }

    /// <summary>
    /// Verifies that QueryAsync applies skip and top parameters to the OData query.
    /// </summary>
    [Fact]
    public async Task QueryAsync_WithSkipAndTop_AppliesSkipAndTop()
    {
        // Arrange
        _boundClient.FindEntriesAsync(Arg.Any<CancellationToken>()).Returns(new List<TestEntity>());

        // Act
        var result = await _sut.QueryAsync(skip: 10, top: 5, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        _boundClient.Received(1).Skip(10L);
        _boundClient.Received(1).Top(5L);
    }

    /// <summary>
    /// Verifies that FindAll returns a success result with all entities.
    /// </summary>
    [Fact]
    public async Task FindAll_ReturnsSuccessWithAllEntities()
    {
        // Arrange
        var entities = new[] { TestEntityBuilder.Default().Build(), TestEntityBuilder.Default().Build() };
        _boundClient.FindEntriesAsync(Arg.Any<CancellationToken>()).Returns(entities);

        // Act
        var result = await _sut.FindAll(CancellationToken.None);

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
        _boundClient.FindScalarAsync<int>(Arg.Any<CancellationToken>()).Returns(42);

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
        _boundClient.FindEntriesAsync(Arg.Any<CancellationToken>()).Returns(entities);

        // Act
        var result = await _sut.QueryAsync(cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
    }

    #endregion

    #region Batch Tests

    /// <summary>
    /// Verifies that AddBatchAsync wraps ODataBatch failures into a failed result.
    /// ODataBatch is a concrete class; when the underlying OData client throws, the exception
    /// is captured and returned as an IntegrationError.
    /// </summary>
    [Fact]
    public async Task AddBatchAsync_WhenClientThrows_ReturnsFailedResult()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            TestEntityBuilder.Default().WithId("batch-1").Build()
        };

        // ODataBatch uses the IODataClient internally; when it throws a NullReferenceException
        // (due to the mock not having real HTTP transport), the exception handler maps it to a failed result.
        // Act
        var result = await _sut.AddBatchAsync(entities, CancellationToken.None);

        // Assert -- batch with mock client fails due to missing HTTP transport; result should be failed, not throw
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.UnexpectedError");
    }

    /// <summary>
    /// Verifies that DeleteBatchAsync wraps ODataBatch failures into a failed result.
    /// </summary>
    [Fact]
    public async Task DeleteBatchAsync_WhenClientThrows_ReturnsFailedResult()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            TestEntityBuilder.Default().WithId("batch-1").Build()
        };

        // Act
        var result = await _sut.DeleteBatchAsync(entities, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.UnexpectedError");
    }

    /// <summary>
    /// Verifies that UpdateBatchAsync wraps ODataBatch failures into a failed result.
    /// </summary>
    [Fact]
    public async Task UpdateBatchAsync_WhenClientThrows_ReturnsFailedResult()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            TestEntityBuilder.Default().WithId("batch-1").Build()
        };

        // Act
        var result = await _sut.UpdateBatchAsync(entities, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("TestEntity.UnexpectedError");
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
        _boundClient
            .Set(Arg.Do<IDictionary<string, object>>(p => capturedPayload = p))
            .Returns(_boundClient);
        _boundClient.InsertEntryAsync(true, Arg.Any<CancellationToken>()).Returns(entity);

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

        var boundClientOData = Substitute.For<IBoundClient<TestEntityWithODataAttributes>>();
        _client.For<TestEntityWithODataAttributes>(null).Returns(boundClientOData);

        IDictionary<string, object>? capturedPayload = null;
        boundClientOData
            .Set(Arg.Do<IDictionary<string, object>>(p => capturedPayload = p))
            .Returns(boundClientOData);
        boundClientOData
            .InsertEntryAsync(true, Arg.Any<CancellationToken>())
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

        var boundClientOData = Substitute.For<IBoundClient<TestEntityWithODataAttributes>>();
        _client.For<TestEntityWithODataAttributes>(null).Returns(boundClientOData);

        IDictionary<string, object>? capturedPayload = null;
        boundClientOData
            .Set(Arg.Do<IDictionary<string, object>>(p => capturedPayload = p))
            .Returns(boundClientOData);
        boundClientOData
            .InsertEntryAsync(true, Arg.Any<CancellationToken>())
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
        _boundClient
            .Set(Arg.Do<IDictionary<string, object>>(p => capturedPayload = p))
            .Returns(_boundClient);
        _boundClient.InsertEntryAsync(true, Arg.Any<CancellationToken>()).Returns(entity);

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

        var boundClientJsonIgnore = Substitute.For<IBoundClient<TestEntityWithJsonIgnore>>();
        _client.For<TestEntityWithJsonIgnore>(null).Returns(boundClientJsonIgnore);

        IDictionary<string, object>? capturedPayload = null;
        boundClientJsonIgnore
            .Set(Arg.Do<IDictionary<string, object>>(p => capturedPayload = p))
            .Returns(boundClientJsonIgnore);
        boundClientJsonIgnore
            .InsertEntryAsync(true, Arg.Any<CancellationToken>())
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

    #endregion
}
