namespace IntegratoR.OData.Domain.Settings;

/// <summary>
/// Settings for API Management gateway authentication and headers.
/// </summary>
public class ODataApiManagementSettings
{
    /// <summary>
    /// Gets or sets the subscription key required by the API gateway.
    /// </summary>
    public string SubscriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the HTTP header used to transmit the subscription key.
    /// Defaults to the Azure API Management standard header.
    /// </summary>
    public string SubscriptionHeaderKey { get; set; } = "Ocp-Apim-Subscription-Key";

    /// <summary>
    /// Gets or sets additional HTTP headers to include with every request via the API gateway.
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();
}
