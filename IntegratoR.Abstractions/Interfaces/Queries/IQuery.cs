using FluentResults;
using IntegratoR.Abstractions.Interfaces.Telemetry;
using MediatR;

namespace IntegratoR.Abstractions.Interfaces.Queries;

/// <summary>
/// Represents a CQRS query that reads system state and returns a response payload.
/// </summary>
/// <typeparam name="TResponse">The type of the response returned by the query handler.</typeparam>
public interface IQuery<out TResponse> : IRequest<TResponse>, IContext
{
}
