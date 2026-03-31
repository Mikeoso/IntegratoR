namespace IntegratoR.OData.Domain.Settings;

/// <summary>
/// Retry and circuit breaker resilience settings for the OData client.
/// </summary>
public class ODataResilienceSettings
{
    /// <summary>
    /// Gets or sets whether automatic retry policies should be enabled for transient failures.
    /// </summary>
    public bool EnableRetries { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of retry attempts for transient failures.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether the circuit breaker pattern should be enabled.
    /// </summary>
    public bool UseCircuitBreaker { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of consecutive failures before the circuit breaker opens.
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the duration in seconds that the circuit breaker stays open before
    /// attempting recovery.
    /// </summary>
    public int CircuitBreakerDurationInSeconds { get; set; } = 30;
}
