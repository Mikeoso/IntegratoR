using FluentResults;
using IntegratoR.Abstractions.Interfaces.Telemetry;
using MediatR;

namespace IntegratoR.Abstractions.Interfaces.Commands;

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
public interface ICommand<out TResponse> : IRequest<TResponse>, IContext
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
public interface ICommand : IRequest<Result>, IContext
{
}
