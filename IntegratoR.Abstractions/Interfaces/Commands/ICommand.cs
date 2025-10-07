using IntegratoR.Abstractions.Interfaces.Results;
using IntegratoR.Abstractions.Interfaces.Telemetry;
using MediatR;

namespace IntegratoR.Abstractions.Interfaces.Commands;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file defines the core command interfaces for the application's CQRS pattern, built upon the
// MediatR library. These interfaces represent the "write" side of the architecture, encapsulating all
// requests that are intended to modify the state of the system (e.g., creating, updating, or deleting
// data).
//
// By routing all state-changing operations through these command interfaces, we create a clear,
// explicit, and auditable flow of control. This pattern allows us to build a robust processing
// pipeline using MediatR behaviors for cross-cutting concerns like validation, logging, and transactions.
// </remarks>
// ---------------------------------------------------------------------------------------------

/// <summary>
/// Represents a command that modifies system state and returns a response payload upon completion.
/// All commands must return a type that implements <see cref="IResult"/> to ensure a standardized response pattern.
/// </summary>
/// <typeparam name="TResponse">The type of the response, which is constrained to be an <see cref="IResult"/>.</typeparam>
/// <remarks>
/// This interface should be used for operations where the caller needs data back after the command is executed.
/// For example, a command to create a new sales order in D365 F&O might implement `ICommand<Result<string>>`
/// to return the newly generated Sales Order ID on success.
/// </remarks>
public interface ICommand<out TResponse> : IRequest<TResponse>, IContext where TResponse : IResult
{
}

/// <summary>
/// Represents a command that modifies system state but does not return a specific value,
/// only an indication of success or failure.
/// </summary>
/// <remarks>
/// This interface is ideal for "fire-and-forget" style operations where the primary concern is successful
/// completion. For instance, a command to trigger a D365 OData Action (like posting an invoice) or
/// updating an existing record would use this interface.
///
/// While it doesn't return a value, its handler still returns an <see cref="IResult"/>, allowing the
/// caller to reliably determine if the operation succeeded or failed, and to access error details
/// in the case of a failure.
/// </remarks>
public interface ICommand : IRequest<IResult>, IContext
{
}