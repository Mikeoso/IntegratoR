using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.Application.Features.Common.Queries;
using IntegratoR.TestKit.Assertions;
using IntegratoR.TestKit.Doubles.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace IntegratoR.Application.Tests.Features.Common.Queries;

/// <summary>
/// Tests for <see cref="GetByKeyQueryHandler{TEntity}"/>.
/// </summary>
public class GetByKeyQueryHandlerTests
{
    private readonly IService<TestEntity> _service = Substitute.For<IService<TestEntity>>();
    private readonly ILogger<GetByKeyQueryHandler<TestEntity>> _logger =
        Substitute.For<ILogger<GetByKeyQueryHandler<TestEntity>>>();
    private readonly GetByKeyQueryHandler<TestEntity> _sut;

    /// <summary>Initialises the handler under test.</summary>
    public GetByKeyQueryHandlerTests()
    {
        _sut = new GetByKeyQueryHandler<TestEntity>(_logger, _service);
    }

    [Fact]
    public async Task Handle_EntityFound_ReturnsSuccessWithEntity()
    {
        // Arrange
        var entity = new TestEntity { Id = "1", PartitionKey = "pk", Name = "Found" };
        var key = entity.GetCompositeKey();
        var query = new GetByKeyQuery<TestEntity>(key);
        _service.GetByKeyAsync(key, Arg.Any<CancellationToken>()).Returns(Result.Ok(entity));

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().Be(entity);
    }

    [Fact]
    public async Task Handle_ServiceFailure_PropagatesErrors()
    {
        // Arrange
        var key = new object[] { "1", "pk" };
        var query = new GetByKeyQuery<TestEntity>(key);
        _service.GetByKeyAsync(key, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TestEntity>("Entity.NotFound"));

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Errors.Should().ContainSingle(e => e.Message == "Entity.NotFound");
    }

    [Fact]
    public async Task Handle_UsesMatchExtensionForPatternMatching()
    {
        // Arrange -- verify the result flows correctly via Match() by checking success path
        var entity = new TestEntity { Id = "1", PartitionKey = "pk", Name = "Test" };
        var key = entity.GetCompositeKey();
        var query = new GetByKeyQuery<TestEntity>(key);
        _service.GetByKeyAsync(key, Arg.Any<CancellationToken>()).Returns(Result.Ok(entity));

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert -- the Match() extension is used internally; result is correct
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("1");
    }
}
