---
paths:
  - "**/*Tests*"
  - "**/*Test*"
---

# .NET Testing

> This file extends [common/testing.md](../common/testing.md) with .NET-specific guidance.

## Framework Stack

- **xUnit** — test framework
- **FluentAssertions** — assertion library (`result.Should().BeSuccess()`)
- **NSubstitute** — mocking library (`Substitute.For<IService<T>>()`)

## Project Naming

Test projects follow the pattern `{ProjectName}.Tests`:
- `IntegratoR.Application.Tests`
- `IntegratoR.OData.Tests`
- `IntegratoR.OData.FO.Tests`

## Testing CQRS Handlers

Every command/query handler returns `Result<T>`. Test both paths:

```csharp
public class CreateCommandHandlerTests
{
    private readonly IService<MyEntity> _service = Substitute.For<IService<MyEntity>>();
    private readonly CreateCommandHandler _sut;

    public CreateCommandHandlerTests()
    {
        _sut = new CreateCommandHandler(_service);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessResult()
    {
        // Arrange
        var entity = new MyEntity { Id = 1, Name = "Test" };
        var command = new CreateCommand<MyEntity>(entity);
        _service.AddAsync(entity, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(entity));

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(entity);
        await _service.Received(1).AddAsync(entity, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ServiceFails_ReturnsFailureResult()
    {
        // Arrange
        var entity = new MyEntity { Id = 1, Name = "Test" };
        var command = new CreateCommand<MyEntity>(entity);
        _service.AddAsync(entity, Arg.Any<CancellationToken>())
            .Returns(Result.Fail("Entity.CreateFailed"));

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
    }
}
```

## Testing Pipeline Behaviours

Mock `RequestHandlerDelegate<TResponse>` to test pass-through and short-circuit:

```csharp
public class LoggingBehaviourTests
{
    private readonly ILogger<LoggingBehaviour<TestCommand, Result>> _logger =
        Substitute.For<ILogger<LoggingBehaviour<TestCommand, Result>>>();

    [Fact]
    public async Task Handle_SuccessfulRequest_LogsStartAndCompletion()
    {
        // Arrange
        var behaviour = new LoggingBehaviour<TestCommand, Result>(_logger);
        var command = new TestCommand();
        var next = Substitute.For<RequestHandlerDelegate<Result>>();
        next().Returns(Result.Ok());

        // Act
        var result = await behaviour.Handle(command, next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await next.Received(1)();
    }
}
```

## Testing FluentValidation Validators

Test validators in isolation — do not run them through the pipeline:

```csharp
public class GetDimensionOrdersQueryValidatorTests
{
    private readonly GetDimensionOrdersQueryValidator _sut = new();

    [Fact]
    public void Validate_EmptyDimensionFormat_HasValidationError()
    {
        // Arrange
        var query = new GetDimensionOrdersQuery(dimensionFormat: "", hierarchyType: HierarchyType.Default);

        // Act
        var result = _sut.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "dimensionFormat");
    }
}
```

## General Guidelines

- Use `CancellationToken.None` in tests unless testing cancellation behaviour
- Use `Arg.Any<CancellationToken>()` when matching NSubstitute calls with cancellation tokens
- Test `IContext.GetLoggingContext()` returns expected keys for entities and commands
- For time-dependent tests, inject `TimeProvider` (or abstract clock) rather than using `DateTime.Now`
