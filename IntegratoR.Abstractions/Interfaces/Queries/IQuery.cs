using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Telemetry;
using MediatR;

namespace IntegratoR.Abstractions.Interfaces.Queries;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file defines the core query interface for the application's CQRS pattern. This interface
// represents the "read" side of the architecture, encapsulating all requests that are intended
// to retrieve data without modifying system state.
//
// By convention, queries should be side-effect-free (idempotent). This strict separation from
// commands simplifies the system design and allows for distinct optimization strategies, such as
// caching, to be applied specifically to the read pipeline.
// </remarks>
// ---------------------------------------------------------------------------------------------

/// <summary>
/// Represents a query operation that reads system state and returns a response payload.
/// This serves as the base contract for all read operations within the CQRS pattern.
/// </summary>
/// <typeparam name="TResponse">The type of the response returned by the query handler.</typeparam>
/// <remarks>
/// Queries are distinct data-retrieval objects, such as `GetCustomerByIdQuery` or `FindSalesOrdersQuery`.
/// Their handlers are responsible for fetching data from a source like D365 F&O and mapping it to a response model.
///
/// The response, <typeparamref name="TResponse"/>, is typically a <see cref="Result{TValue}"/> object,
/// ensuring that the caller can gracefully handle both successful data retrieval and potential issues
/// like a "not found" scenario or a data access error.
/// </remarks>
public interface IQuery<out TResponse> : IRequest<TResponse>, IContext
{
}