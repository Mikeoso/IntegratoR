namespace IntegratoR.OData.Domain.Settings;

/// <summary>
/// Represents the retry and circuit breaker resilience settings for the OData client.
/// </summary>
public class ODataResilienceSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether automatic retry policies are enabled for transient failures.
    /// </summary>
    /// <value>The default value is <see langword="true"/>.</value>
    public bool EnableRetries { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of retry attempts for transient failures.
    /// </summary>
    /// <value>The default value is <c>3</c>; the valid range is 1 to 10.</value>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets a value indicating whether the circuit breaker pattern is enabled.
    /// </summary>
    /// <value>The default value is <see langword="true"/>.</value>
    public bool UseCircuitBreaker { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of consecutive failures before the circuit breaker opens.
    /// </summary>
    /// <value>The default value is <c>5</c>.</value>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the duration in seconds that the circuit breaker stays open before attempting recovery.
    /// </summary>
    /// <value>The default value is <c>30</c>.</value>
    public int CircuitBreakerDurationInSeconds { get; set; } = 30;
}
