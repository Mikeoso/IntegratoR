using System.Diagnostics;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Telemetry;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.Application.Common.Behaviours;

/// <summary>
/// Provides a MediatR pipeline behaviour that adds structured logging and execution timing to every request.
/// </summary>
/// <typeparam name="TRequest">The type of the MediatR request being handled.</typeparam>
/// <typeparam name="TResponse">The type of the response from the request handler.</typeparam>
/// <remarks>A failed <see cref="IResultBase"/> is logged at <see cref="LogLevel.Warning"/>; an unhandled exception is logged at <see cref="LogLevel.Error"/> and rethrown.</remarks>
public class LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IContext
{
    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingBehaviour{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostics.</param>
    public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Asynchronously wraps request execution with structured logging and timing.
    /// </summary>
    /// <param name="request">The incoming MediatR request.</param>
    /// <param name="next">A delegate that invokes the next behaviour or the request handler.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The response from the next handler in the pipeline.</returns>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var contextDictionary = request.GetLoggingContext();

        using (_logger.BeginScope(contextDictionary))
        {
            var requestName = typeof(TRequest).Name;
            _logger.LogInformation("Handling {RequestName}", requestName);
            // The full request payload is intentionally logged only at Debug (off by default in
            // production) to avoid emitting PII / secrets to Information-level sinks such as Application Insights.
            _logger.LogDebug("Request payload for {RequestName}: {@Request}", requestName, request);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = await next().ConfigureAwait(false);
                stopwatch.Stop();

                if (response is IResultBase { IsFailed: true } baseResult)
                {
                    _logger.LogWarning(
                        "Handled {RequestName} with failure result in {ElapsedMilliseconds}ms. Error: {ErrorCode} - {ErrorMessage}",
                        requestName,
                        stopwatch.ElapsedMilliseconds,
                        baseResult.GetError()?.Code,
                        baseResult.GetError()?.Message);
                }
                else
                {
                    _logger.LogInformation(
                        "Handled {RequestName} successfully in {ElapsedMilliseconds}ms",
                        requestName,
                        stopwatch.ElapsedMilliseconds);
                }
                _logger.LogDebug("Response for {RequestName}: {@Response}", requestName, response);
                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "An unhandled exception occurred while handling {RequestName} after {ElapsedMilliseconds}ms.", requestName, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
