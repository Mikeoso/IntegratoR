using FluentResults;
using IntegratoR.Abstractions.Interfaces.Telemetry;
using MediatR;

namespace IntegratoR.Abstractions.Interfaces.Commands;

/// <summary>
/// Represents a CQRS command that modifies system state and returns a response payload upon completion.
/// </summary>
/// <typeparam name="TResponse">The type of the response returned by the command handler.</typeparam>
public interface ICommand<out TResponse> : IRequest<TResponse>, IContext
{
}

/// <summary>
/// Represents a CQRS command that modifies system state and reports only success or failure.
/// </summary>
public interface ICommand : IRequest<Result>, IContext
{
}
