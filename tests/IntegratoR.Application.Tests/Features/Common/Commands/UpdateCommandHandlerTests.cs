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
/// Tests for <see cref="UpdateCommandHandler{TEntity}"/>.
/// </summary>
public class UpdateCommandHandlerTests
{
    private readonly IService<TestEntity> _service = Substitute.For<IService<TestEntity>>();
    private readonly UpdateCommandHandler<TestEntity> _sut;

    /// <summary>Initialises the handler under test.</summary>
    public UpdateCommandHandlerTests()
    {
        _sut = new UpdateCommandHandler<TestEntity>(_service);
    }

    [Fact]
    public async Task Handle_ValidCommand_DelegatesToServiceUpdateAsync()
    {
        // Arrange
        var entity = new TestEntity { Id = "1", PartitionKey = "pk", Name = "Updated" };
        var command = new UpdateCommand<TestEntity>(entity);
        _service.UpdateAsync(entity, Arg.Any<CancellationToken>()).Returns(Result.Ok(entity));

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        await _service.Received(1).UpdateAsync(entity, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ServiceReturnsSuccess_ReturnsSuccessResult()
    {
        // Arrange
        var entity = new TestEntity { Id = "1", PartitionKey = "pk", Name = "Updated" };
        var command = new UpdateCommand<TestEntity>(entity);
        _service.UpdateAsync(entity, Arg.Any<CancellationToken>()).Returns(Result.Ok(entity));

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().Be(entity);
    }

    [Fact]
    public async Task Handle_ServiceReturnsFailure_PropagatesErrors()
    {
        // Arrange
        var entity = new TestEntity { Id = "1", PartitionKey = "pk", Name = "Updated" };
        var command = new UpdateCommand<TestEntity>(entity);
        _service.UpdateAsync(entity, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TestEntity>("Entity.UpdateFailed"));

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Errors.Should().ContainSingle(e => e.Message == "Entity.UpdateFailed");
    }
}
