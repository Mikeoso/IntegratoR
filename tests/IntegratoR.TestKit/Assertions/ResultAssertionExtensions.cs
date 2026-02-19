using FluentResults;

namespace IntegratoR.TestKit.Assertions;

/// <summary>
/// Provides extension methods that allow <see cref="Result"/> and <see cref="Result{T}"/>
/// objects to be chained with custom FluentAssertions assertion types.
/// </summary>
public static class ResultAssertionExtensions
{
    /// <summary>
    /// Returns a <see cref="ResultAssertions"/> object for asserting on a non-generic <see cref="Result"/>.
    /// </summary>
    /// <param name="result">The result to assert on.</param>
    /// <returns>A <see cref="ResultAssertions"/> instance bound to the given result.</returns>
    public static ResultAssertions Should(this Result result)
        => new(result);

    /// <summary>
    /// Returns a <see cref="ResultAssertions{T}"/> object for asserting on a generic <see cref="Result{T}"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the result.</typeparam>
    /// <param name="result">The result to assert on.</param>
    /// <returns>A <see cref="ResultAssertions{T}"/> instance bound to the given result.</returns>
    public static ResultAssertions<T> Should<T>(this Result<T> result)
        => new(result);
}
