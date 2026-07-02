namespace IntegratoR.OData.Domain.Settings;

/// <summary>
/// Represents all configuration settings required to connect and authenticate with a D365 F&amp;O OData endpoint, including retry and resilience policies.
/// </summary>
public class ODataSettings
{
    /// <summary>
    /// Gets or sets the base URL of the D365 F&amp;O OData endpoint.
    /// </summary>
    /// <example>https://your-environment.operations.dynamics.com/data</example>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timeout in seconds for individual HTTP requests to the OData service.
    /// </summary>
    /// <value>The default value is <c>120</c>.</value>
    public double Timeout { get; set; } = 120;

    /// <summary>
    /// Gets or sets the path to a local <c>metadata.xml</c> file to use instead of fetching metadata from the server.
    /// </summary>
    public string? MetadataFilePath { get; set; }

    /// <summary>
    /// Gets or sets the authentication settings for the OData client.
    /// </summary>
    public ODataAuthenticationSettings Authentication { get; set; } = new();

    /// <summary>
    /// Gets or sets the resilience settings (retry and circuit breaker) for the OData client.
    /// </summary>
    public ODataResilienceSettings Resilience { get; set; } = new();

    /// <summary>
    /// Gets or sets the batch write settings (failure mode and automatic chunking) for the OData client.
    /// </summary>
    public ODataBatchSettings Batch { get; set; } = new();
}
