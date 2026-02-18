using FluentAssertions;
using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Application.Common.Behaviours;
using MediatR;
using NSubstitute;
using Xunit;

namespace IntegratoR.Application.Tests.Common.Behaviours;

/// <summary>
/// Tests for <see cref="ValidationBehaviour{TRequest,TResponse}"/>.
/// </summary>
public class ValidationBehaviourTests
{
    /// <summary>A test request returning a generic result.</summary>
    public record TestGenericRequest : IRequest<Result<string>>;

    /// <summary>A test request returning a non-generic result.</summary>
    public record TestNonGenericRequest : IRequest<Result>;

    [Fact]
    public async Task Handle_NoValidators_PassesThroughToNextHandler()
    {
        // Arrange
        var sut = new ValidationBehaviour<TestGenericRequest, Result<string>>(
            Enumerable.Empty<IValidator<TestGenericRequest>>());
        var request = new TestGenericRequest();
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        next(Arg.Any<CancellationToken>()).Returns(Result.Ok("value"));

        // Act
        var result = await sut.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await next.Received(1)(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_PassesThroughToNextHandler()
    {
        // Arrange
        var validator = Substitute.For<IValidator<TestGenericRequest>>();
        validator.Validate(Arg.Any<ValidationContext<TestGenericRequest>>())
            .Returns(new ValidationResult());
        var sut = new ValidationBehaviour<TestGenericRequest, Result<string>>(
            new[] { validator });
        var request = new TestGenericRequest();
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        next(Arg.Any<CancellationToken>()).Returns(Result.Ok("value"));

        // Act
        var result = await sut.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await next.Received(1)(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidRequest_ShortCircuitsWithValidationError()
    {
        // Arrange
        var validator = Substitute.For<IValidator<TestGenericRequest>>();
        validator.Validate(Arg.Any<ValidationContext<TestGenericRequest>>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Entity", "Entity is required.") }));
        var sut = new ValidationBehaviour<TestGenericRequest, Result<string>>(new[] { validator });
        var request = new TestGenericRequest();
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();

        // Act
        var result = await sut.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        await next.DidNotReceive()(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidRequest_ErrorCodeIsValidationError()
    {
        // Arrange
        var validator = Substitute.For<IValidator<TestGenericRequest>>();
        validator.Validate(Arg.Any<ValidationContext<TestGenericRequest>>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Entity", "Entity is required.") }));
        var sut = new ValidationBehaviour<TestGenericRequest, Result<string>>(new[] { validator });
        var request = new TestGenericRequest();
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();

        // Act
        var result = await sut.Handle(request, next, CancellationToken.None);

        // Assert
        var error = result.Errors.OfType<IntegrationError>().First();
        error.Code.Should().Be("Validation.Error");
    }

    [Fact]
    public async Task Handle_InvalidRequest_ErrorTypeIsValidation()
    {
        // Arrange
        var validator = Substitute.For<IValidator<TestGenericRequest>>();
        validator.Validate(Arg.Any<ValidationContext<TestGenericRequest>>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Entity", "Entity is required.") }));
        var sut = new ValidationBehaviour<TestGenericRequest, Result<string>>(new[] { validator });
        var request = new TestGenericRequest();
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();

        // Act
        var result = await sut.Handle(request, next, CancellationToken.None);

        // Assert
        var error = result.Errors.OfType<IntegrationError>().First();
        error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_GenericResultResponse_CreatesCorrectGenericFailType()
    {
        // Arrange
        var validator = Substitute.For<IValidator<TestGenericRequest>>();
        validator.Validate(Arg.Any<ValidationContext<TestGenericRequest>>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Entity", "Error") }));
        var sut = new ValidationBehaviour<TestGenericRequest, Result<string>>(new[] { validator });
        var request = new TestGenericRequest();
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();

        // Act
        var result = await sut.Handle(request, next, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Result<string>>();
        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NonGenericResultResponse_CreatesCorrectNonGenericFailType()
    {
        // Arrange
        var validator = Substitute.For<IValidator<TestNonGenericRequest>>();
        validator.Validate(Arg.Any<ValidationContext<TestNonGenericRequest>>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Entity", "Error") }));
        var sut = new ValidationBehaviour<TestNonGenericRequest, Result>(new[] { validator });
        var request = new TestNonGenericRequest();
        var next = Substitute.For<RequestHandlerDelegate<Result>>();

        // Act
        var result = await sut.Handle(request, next, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Result>();
        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MultipleValidationFailures_UsesFirstFailureOnly()
    {
        // Arrange
        var validator = Substitute.For<IValidator<TestGenericRequest>>();
        validator.Validate(Arg.Any<ValidationContext<TestGenericRequest>>())
            .Returns(new ValidationResult(new[]
            {
                new ValidationFailure("Field1", "First error message"),
                new ValidationFailure("Field2", "Second error message")
            }));
        var sut = new ValidationBehaviour<TestGenericRequest, Result<string>>(new[] { validator });
        var request = new TestGenericRequest();
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();

        // Act
        var result = await sut.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().HaveCount(1);
        result.Errors.First().Message.Should().Be("First error message");
    }
}
