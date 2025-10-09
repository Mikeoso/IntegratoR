using IntegratoR.Abstractions.Common.Results;

namespace IntegratoR.Abstractions.Interfaces.Results;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file defines the public contracts for the application's Result pattern.
// By depending on these interfaces instead of concrete implementations, we adhere to the
// Dependency Inversion Principle. This promotes loose coupling, making our application
// layers (e.g., domain, application, infrastructure) more modular, testable, and easier to maintain.
// </remarks>
// ---------------------------------------------------------------------------------------------

/// <summary>
/// Defines the core contract for the result of an operation, explicitly representing its success or failure.
/// This serves as the universal return type for any operation that can fail.
/// </summary>
/// <remarks>
/// A method signature returning <see cref="IResult"/> clearly communicates that the operation has a binary
/// outcome and does not return a value on success. This is the standard return contract for valueless
/// CQRS Commands (e.g., Update or Delete operations).
/// </remarks>
public interface IResult
{
    /// <summary>
    /// Gets a value indicating whether the operation completed successfully.
    /// </summary>
    bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed. This is the logical inverse of <see cref="IsSuccess"/>.
    /// </summary>
    bool IsFailure { get; }

    /// <summary>
    /// Gets the <see cref="Error"/> object if the operation failed. Returns <see langword="null"/> on success.
    /// </summary>
    Error? Error { get; }
}

/// <summary>
/// Defines the contract for the result of an operation that returns a value on success.
/// </summary>
/// <typeparam name="TValue">The type of the value returned by a successful operation. The 'out' keyword indicates covariance.</typeparam>
/// <remarks>
/// This is the standard return contract for operations that retrieve data, such as CQRS Queries.
/// The covariance of <typeparamref name="TValue"/> allows for greater flexibility, for example,
/// assigning an <c>IResult&lt;string&gt;</c> to a variable of type <c>IResult&lt;object&gt;</c>.
/// </remarks>
public interface IResult<out TValue> : IResult
{
    /// <summary>
    /// Gets the value returned by the operation on success.
    /// </summary>
    /// <remarks>
    /// Caution: This property will be <see langword="null"/> or <c>default</c> if the operation failed.
    /// Always check the <see cref="IResult.IsSuccess"/> flag before accessing this property directly.
    /// </remarks>
    TValue? Value { get; }
}