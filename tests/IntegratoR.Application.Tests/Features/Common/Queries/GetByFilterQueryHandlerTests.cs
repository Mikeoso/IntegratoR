using System.Linq.Expressions;
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
/// Tests for <see cref="GetByFilterQueryHandler{TEntity}"/>.
/// </summary>
public class GetByFilterQueryHandlerTests
{
    private readonly IService<TestEntity> _service = Substitute.For<IService<TestEntity>>();
    private readonly ILogger<GetByFilterQueryHandler<TestEntity>> _logger =
        Substitute.For<ILogger<GetByFilterQueryHandler<TestEntity>>>();
    private readonly GetByFilterQueryHandler<TestEntity> _sut;

    /// <summary>Initialises the handler under test.</summary>
    public GetByFilterQueryHandlerTests()
    {
        _sut = new GetByFilterQueryHandler<TestEntity>(_logger, _service);
    }

    [Fact]
    public async Task Handle_EntitiesFound_ReturnsSuccessWithMaterializedList()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new() { Id = "1", PartitionKey = "pk", Name = "Alpha" },
            new() { Id = "2", PartitionKey = "pk", Name = "Beta" }
        };
        Expression<Func<TestEntity, bool>> filter = e => e.Name != null;
        var query = new GetByFilterQuery<TestEntity>(filter);
        _service.FindAsync(filter, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IEnumerable<TestEntity>>(entities));

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ServiceFailure_PropagatesErrors()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> filter = e => e.Id == "missing";
        var query = new GetByFilterQuery<TestEntity>(filter);
        _service.FindAsync(filter, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IEnumerable<TestEntity>>("Filter.Failed"));

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Errors.Should().ContainSingle(e => e.Message == "Filter.Failed");
    }

    [Fact]
    public async Task Handle_EmptyResult_ReturnsSuccessWithEmptyCollection()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> filter = e => e.Id == "nonexistent";
        var query = new GetByFilterQuery<TestEntity>(filter);
        _service.FindAsync(filter, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IEnumerable<TestEntity>>(Enumerable.Empty<TestEntity>()));

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().BeEmpty();
    }
}
