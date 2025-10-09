namespace IntegratoR.Abstractions.Interfaces.Telemetry;

/// <summary>
/// Defines a contract for CQRS requests that can provide
/// key-value pairs for enriching structured logs within a logging scope.
/// </summary>
public interface IContext
{
    /// <summary>
    /// Gets a dictionary of context properties for logging.
    /// </summary>
    /// <returns>A dictionary of key-value pairs to be added to the logging scope.</returns>
    IReadOnlyDictionary<string, object> GetLoggingContext();
}
