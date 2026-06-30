using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using Xunit;

namespace IntegratoR.Abstractions.Tests.Common.Results;

/// <summary>
/// Tests for <see cref="ResultFactory"/> — the cached-reflection failure factory that builds the
/// correctly-typed failed result (generic <see cref="Result{T}"/> or non-generic <see cref="Result"/>)
/// from an <see cref="IError"/> when the concrete result type is only known via a generic parameter.
/// </summary>
public class ResultFactoryTests
{
    /// <summary>A throwaway value type used to close <see cref="Result{T}"/>.</summary>
    private sealed record SomeType(string Value);

    [Fact]
    public void FailFromError_GenericResult_ProducesClosedGenericFailedResult()
    {
        // Arrange
        var error = new IntegrationError("Some.Code", "some message", ErrorType.Failure);

        // Act
        Result<SomeType> result = ResultFactory.FailFromError<Result<SomeType>>(error);

        // Assert
        result.Should().BeOfType<Result<SomeType>>();
        result.IsFailed.Should().BeTrue();
        result.GetError()!.Code.Should().Be("Some.Code");
    }

    [Fact]
    public void FailFromError_NonGenericResult_ProducesNonGenericFailedResult()
    {
        // Arrange
        var error = new IntegrationError("Some.Code", "some message", ErrorType.Failure);

        // Act
        Result result = ResultFactory.FailFromError<Result>(error);

        // Assert
        result.Should().BeOfType<Result>();
        result.IsFailed.Should().BeTrue();
        result.GetError()!.Code.Should().Be("Some.Code");
    }
}
