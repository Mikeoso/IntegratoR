using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;

namespace IntegratoR.TestKit.Assertions;

/// <summary>
/// Provides custom FluentAssertions assertions for non-generic <see cref="Result"/> objects,
/// enabling readable assertions such as <c>.Should().BeSuccessful()</c> and <c>.Should().HaveErrorCode("ERR001")</c>.
/// </summary>
public sealed class ResultAssertions : ReferenceTypeAssertions<Result, ResultAssertions>
{
    /// <summary>
    /// Initialises a new instance of <see cref="ResultAssertions"/> for the given <paramref name="subject"/>.
    /// </summary>
    /// <param name="subject">The <see cref="Result"/> to assert on.</param>
    public ResultAssertions(Result subject)
        : base(subject, AssertionChain.GetOrCreate())
    {
    }

    /// <inheritdoc/>
    protected override string Identifier => "Result";

    /// <summary>
    /// Asserts that the result is successful (<see cref="Result.IsSuccess"/> is <c>true</c>).
    /// </summary>
    /// <param name="because">Optional reason for the assertion, for failure messages.</param>
    /// <param name="becauseArgs">Optional arguments to format into <paramref name="because"/>.</param>
    /// <returns>An <see cref="AndConstraint{T}"/> for chaining further assertions.</returns>
    public AndConstraint<ResultAssertions> BeSuccessful(string because = "", params object[] becauseArgs)
    {
        CurrentAssertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsSuccess)
            .FailWith("Expected result to be successful{reason}, but it failed with: {0}",
                string.Join(", ", Subject.Errors.Select(e => e.Message)));

        return new AndConstraint<ResultAssertions>(this);
    }

    /// <summary>
    /// Asserts that the result has failed (<see cref="Result.IsFailed"/> is <c>true</c>).
    /// </summary>
    /// <param name="because">Optional reason for the assertion, for failure messages.</param>
    /// <param name="becauseArgs">Optional arguments to format into <paramref name="because"/>.</param>
    /// <returns>An <see cref="AndConstraint{T}"/> for chaining further assertions.</returns>
    public AndConstraint<ResultAssertions> BeFailed(string because = "", params object[] becauseArgs)
    {
        CurrentAssertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsFailed)
            .FailWith("Expected result to have failed{reason}, but it succeeded.");

        return new AndConstraint<ResultAssertions>(this);
    }

    /// <summary>
    /// Asserts that the result contains an <see cref="IntegrationError"/> with the specified error code.
    /// </summary>
    /// <param name="expectedCode">The expected error code value.</param>
    /// <param name="because">Optional reason for the assertion, for failure messages.</param>
    /// <param name="becauseArgs">Optional arguments to format into <paramref name="because"/>.</param>
    /// <returns>An <see cref="AndConstraint{T}"/> for chaining further assertions.</returns>
    public AndConstraint<ResultAssertions> HaveErrorCode(string expectedCode, string because = "", params object[] becauseArgs)
    {
        var error = Subject.Errors.OfType<IntegrationError>().FirstOrDefault();

        CurrentAssertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(error is not null)
            .FailWith("Expected result to contain an IntegrationError{reason}, but none was found.")
            .Then
            .ForCondition(error?.Code == expectedCode)
            .FailWith("Expected error code to be {0}{reason}, but found {1}.", expectedCode, error?.Code);

        return new AndConstraint<ResultAssertions>(this);
    }

    /// <summary>
    /// Asserts that the result contains an <see cref="IntegrationError"/> with the specified <see cref="ErrorType"/>.
    /// </summary>
    /// <param name="expectedType">The expected <see cref="ErrorType"/>.</param>
    /// <param name="because">Optional reason for the assertion, for failure messages.</param>
    /// <param name="becauseArgs">Optional arguments to format into <paramref name="because"/>.</param>
    /// <returns>An <see cref="AndConstraint{T}"/> for chaining further assertions.</returns>
    public AndConstraint<ResultAssertions> HaveErrorType(ErrorType expectedType, string because = "", params object[] becauseArgs)
    {
        var error = Subject.Errors.OfType<IntegrationError>().FirstOrDefault();

        CurrentAssertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(error is not null)
            .FailWith("Expected result to contain an IntegrationError{reason}, but none was found.")
            .Then
            .ForCondition(error?.Type == expectedType)
            .FailWith("Expected error type to be {0}{reason}, but found {1}.", expectedType, error?.Type);

        return new AndConstraint<ResultAssertions>(this);
    }
}
