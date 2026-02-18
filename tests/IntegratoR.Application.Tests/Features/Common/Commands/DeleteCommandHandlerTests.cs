using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.Application.Features.Common.Commands;
using IntegratoR.TestKit.Assertions;
using IntegratoR.TestKit.Doubles.Entities;
using NSubstitute;
using Xunit;

namespace IntegratoR.Application.Tests.Features.Common.Commands;

/// <summary>
/// Tests for <see cref="DeleteCommandHandler{TEntity}"/>.
/// </summary>
public class DeleteCommandHandlerTests
{
    private readonly IService<TestEntity> _service = Substitute.For<IService<TestEntity>>();
    private readonly DeleteCommandHandler<TestEntity> _sut;

    /// <summary>Initialises the handler under test.</summary>
    public DeleteCommandHandlerTests()
    {
        _sut = new DeleteCommandHandler<TestEntity>(_service);
    }

    [Fact]
    public async Task Handle_ServiceDeleteSucceeds_ReturnsOkWithOriginalEntity()
    {
        // Arrange
        var entity = new TestEntity { Id = "1", PartitionKey = "pk", Name = "ToDelete" };
        var command = new DeleteCommand<TestEntity>(entity);
        _service.DeleteAsync(entity, Arg.Any<CancellationToken>()).Returns(Result.Ok());

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().Be(entity);
    }

    [Fact]
    public async Task Handle_ServiceDeleteFails_PropagatesErrors()
    {
        // Arrange
        var entity = new TestEntity { Id = "1", PartitionKey = "pk", Name = "ToDelete" };
        var command = new DeleteCommand<TestEntity>(entity);
        _service.DeleteAsync(entity, Arg.Any<CancellationToken>())
            .Returns(Result.Fail("Entity.DeleteFailed"));

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Errors.Should().ContainSingle(e => e.Message == "Entity.DeleteFailed");
    }

    [Fact]
    public async Task Handle_SuccessResult_ConvertsNonGenericToGenericResultWithEntity()
    {
        // Arrange
        var entity = new TestEntity { Id = "2", PartitionKey = "pk2", Name = "ConvertTest" };
        var command = new DeleteCommand<TestEntity>(entity);
        _service.DeleteAsync(entity, Arg.Any<CancellationToken>()).Returns(Result.Ok());

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert -- Result<TEntity> with original entity, not Result.Fail
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(entity);
    }
}
