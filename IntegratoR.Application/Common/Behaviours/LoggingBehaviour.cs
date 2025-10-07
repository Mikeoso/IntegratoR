using IntegratoR.Abstractions.Interfaces.Results;
using IntegratoR.Abstractions.Interfaces.Telemetry;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace IntegratoR.Application.Common.Behaviours;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file defines a critical component for application observability. In a distributed architecture
// like one using Azure Functions and D365, having consistent, structured, and centralized logging
// is not a luxury—it's a necessity for debugging and monitoring. This MediatR behavior provides
// that consistency for every command and query in the system.
// </remarks>
// ---------------------------------------------------------------------------------------------

/// <summary>
/// A MediatR pipeline behavior that provides consistent, structured, and performance-aware logging
/// for all requests. It logs the start and outcome of each request, measures execution time, and
/// intelligently distinguishes between successful operations, controlled failures (via the <see cref="IResult"/> pattern),
/// and unexpected exceptions.
/// </summary>
/// <typeparam name="TRequest">The type of the MediatR request being handled.</typeparam>
/// <typeparam name="TResponse">The type of the response from the request handler.</typeparam>
/// <remarks>
/// This behavior ensures a uniform logging format across the entire application, which is invaluable
/// for production monitoring. The use of structured logging placeholders (e.g., `{@Request}`) is
/// specifically designed to integrate with modern logging platforms like **Azure Application Insights**,
/// Serilog, or Seq. This allows for powerful querying, filtering, and alerting on log data.
///
/// By differentiating between `Warning` for controlled failures and `Error` for unhandled exceptions,
/// it enables more accurate and less noisy operational alerts.
/// </remarks>
public class LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IContext
{
    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingBehaviour{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="logger">The logger instance, injected via dependency injection.</param>
    public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Intercepts a request to wrap its execution with comprehensive logging and performance timing.
    /// </summary>
    /// <param name="request">The incoming MediatR request object.</param>
    /// <param name="next">A delegate representing the next action in the pipeline, which eventually calls the handler.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The response from the next handler in the pipeline.</returns>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var contextDictionary = request.GetContextForLogging();

        using (_logger.BeginScope(contextDictionary))
        {
            var requestName = typeof(TRequest).Name;
            _logger.LogInformation("Handling {RequestName} with data: {@Request}", requestName, request);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = await next();
                stopwatch.Stop();

                // Check if the response is a controlled failure (IResult)
                if (response is IResult { IsFailure: true } result)
                {
                    // Log controlled failures as Warnings, as they are expected failure paths.
                    _logger.LogWarning(
                        "Handled {RequestName} with failure result in {ElapsedMilliseconds}ms. Error: {ErrorCode} - {ErrorMessage}",
                        requestName,
                        stopwatch.ElapsedMilliseconds,
                        result.Error?.Code,
                        result.Error?.Message);
                }
                else
                {
                    // Log successful operations as Information.
                    _logger.LogInformation(
                        "Handled {RequestName} successfully in {ElapsedMilliseconds}ms",
                        requestName,
                        stopwatch.ElapsedMilliseconds);
                }
                // Log the full response at the Debug level for detailed diagnostics without cluttering standard logs.
                _logger.LogDebug("Response for {RequestName}: {@Response}", requestName, response);
                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                // Log unhandled exceptions as Errors, as they represent unexpected states.
                _logger.LogError(ex, "An unhandled exception occurred while handling {RequestName} after {ElapsedMilliseconds}ms.", requestName, stopwatch.ElapsedMilliseconds);

                // Re-throw the exception to allow higher-level exception handlers
                // (e.g., global exception middleware in Azure Functions) to process it.
                throw;
            }
        }

    }
}