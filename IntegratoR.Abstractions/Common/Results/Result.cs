using IntegratoR.Abstractions.Common.Result;
using IntegratoR.Abstractions.Interfaces.Results;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace IntegratoR.Abstractions.Common.Results;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file defines the Result pattern, a core building block for our integration architecture.
// By encapsulating the outcome of an operation (either success or failure) into a single return type,
// we avoid using exceptions for control flow. This leads to cleaner, more predictable, and more
// resilient code, which is especially critical in distributed systems like Azure Functions
// interacting with D365 F&O. It forces the caller to explicitly handle both success and failure
// states, preventing common runtime errors and making functional pipelines more explicit.
// </remarks>
// ---------------------------------------------------------------------------------------------


/// <summary>
/// Represents the outcome of an operation that does not return a value, indicating either success or failure.
/// </summary>
/// <remarks>
/// This non-generic version is ideal for CQRS Commands or repository methods where the operation's success
/// is the only required information (e.g., Create, Update, Delete, or invoking a OData Action).
/// </remarks>
public class Result : IResult
{
    /// <inheritdoc />
    public bool IsSuccess { get; }

    /// <inheritdoc />
    public bool IsFailure => !IsSuccess;

    /// <inheritdoc />
    public Error? Error { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Result"/> class, enforcing the pattern's invariants.
    /// </summary>
    /// <param name="isSuccess">A flag indicating if the operation was successful.</param>
    /// <param name="error">The error object describing the failure. Must be null for a success result.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown during construction if the rules of the Result pattern are violated,
    /// such as creating a success result with an error or a failure result without one.
    /// </exception>
    public Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error is not null)
        {
            // This is a developer error, not a runtime error. The system should fail fast.
            throw new InvalidOperationException("A successful result cannot have an error.");
        }

        if (!isSuccess && error is null)
        {
            // A failure must always provide a reason.
            throw new InvalidOperationException("A failure result must have an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Creates a success <see cref="Result"/>.
    /// </summary>
    /// <returns>A new instance of <see cref="Result"/> indicating a successful operation.</returns>
    public static Result Ok() => new(true, null);

    /// <summary>
    /// Creates a failure <see cref="Result"/> with the specified error.
    /// </summary>
    /// <param name="error">The error object that provides context about the failure.</param>
    /// <returns>A new instance of <see cref="Result"/> indicating a failed operation.</returns>
    public static Result Fail(Error error) => new(false, error);

    /// <summary>
    /// Provides a functional way to process the result by executing one of two provided actions.
    /// </summary>
    /// <typeparam name="TOut">The return type of the provided functions.</typeparam>
    /// <param name="onSuccess">The function to execute if the result is a success.</param>
    /// <param name="onFailure">The function to execute if the result is a failure. The error object is passed as an argument.</param>
    /// <returns>The value returned by whichever function is executed.</returns>
    /// <remarks>
    /// Using Match is the preferred way to handle a Result. It enforces that both success and failure paths
    /// are considered, preventing developers from forgetting to check the <see cref="IsFailure"/> flag.
    /// </remarks>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess() : onFailure(Error!);
}

/// <summary>
/// Represents the result of an operation that returns a value, indicating success or failure.
/// </summary>
/// <typeparam name="TValue">The type of the value returned by a successful operation.</typeparam>
/// <remarks>
/// This generic version is the standard return type for CQRS Queries or any function that retrieves data,
/// such as fetching a data entity from D365 F&O.
/// </remarks>
public sealed class Result<TValue> : Result, IResult<TValue>
{
    /// <inheritdoc />
    /// <remarks>
    /// Caution: This property is null when <see cref="Result.IsFailure"/> is true.
    /// Always check <see cref="Result.IsSuccess"/> before accessing the value directly.
    /// To avoid potential null reference exceptions, prefer using the <see cref="Match{TOut}"/> method for safer access.
    /// </remarks>
    [MaybeNull]
    public TValue Value { get; }

    /// <summary>
    /// Initializes a new instance of the generic <see cref="Result{TValue}"/> class.
    /// </summary>
    public Result(bool isSuccess, TValue? value, Error? error) : base(isSuccess, error)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a success result containing the specified value.
    /// </summary>
    /// <param name="value">The payload value to be wrapped in the result.</param>
    /// <returns>A new success instance of <see cref="Result{TValue}"/>.</returns>
    public static Result<TValue> Ok(TValue value) => new(true, value, null);

    /// <summary>
    /// Creates a failure result with the specified error.
    /// </summary>
    /// <param name="error">The error object that provides context about the failure.</param>
    /// <returns>A new failure instance of <see cref="Result{TValue}"/>.</returns>
    public static new Result<TValue> Fail(Error error) => new(false, default, error);

    /// <summary>
    /// Creates a new failure result from an existing non-generic failure result, preserving the error.
    /// </summary>
    /// <param name="failureResult">The non-generic result object from which to copy the error.</param>
    /// <returns>A new failure instance of <see cref="Result{TValue}"/> with the same error.</returns>
    /// <exception cref="ArgumentException">Thrown if the provided <paramref name="failureResult"/> is actually a success result.</exception>
    /// <remarks>
    /// This is a highly useful utility for mapping between layers. For example, if a validation step
    /// returns a non-generic `Result.Fail(error)`, you can easily convert it to a `Result<CustomerDto>.Fail(error)`
    /// at the API boundary without losing the original error context.
    /// </remarks>
    public static Result<TValue> Fail(Result failureResult)
    {
        if (failureResult.IsSuccess)
        {
            throw new ArgumentException("Cannot create a failure result from a success result.", nameof(failureResult));
        }

        return Fail(failureResult.Error!);
    }

    /// <summary>
    /// Provides a functional way to process the result by executing one of two provided functions.
    /// </summary>
    /// <typeparam name="TOut">The return type of the provided functions.</typeparam>
    /// <param name="onSuccess">The function to execute if the result is a success. The result's value is passed as an argument.</param>
    /// <param name="onFailure">The function to execute if the result is a failure. The error object is passed as an argument.</param>
    /// <returns>The value returned by whichever function is executed.</returns>
    public TOut Match<TOut>(Func<TValue, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess(Value!) : onFailure(Error!);

    /// <summary>
    /// Implicitly converts a value to a success <see cref="Result{TValue}"/>.
    /// </summary>
    /// <remarks>
    /// This allows for returning a value directly from a method that has a `Result<TValue>` return type,
    /// for example: `return myCustomer;` instead of `return Result.Ok(myCustomer);`.
    /// While convenient, ensure your team has a clear convention for its use, as implicit operators
    /// can sometimes make code less obvious to developers unfamiliar with the pattern.
    /// </remarks>
    public static implicit operator Result<TValue>(TValue value) => Ok(value);

    /// <summary>
    /// Implicitly converts an <see cref="Error"/> to a failure <see cref="Result{TValue}"/>.
    /// </summary>
    /// <remarks>
    /// This allows for returning an error directly from a method that has a `Result<TValue>` return type,
    /// for example: `return Errors.NotFound;` instead of `return Result.Fail(Errors.NotFound);`.
    /// </remarks>
    public static implicit operator Result<TValue>(Error error) => Fail(error);
}