namespace IntegratoR.OData.Domain.Settings;

/// <summary>
/// Represents the API Management gateway authentication and header settings for the OData client.
/// </summary>
public class ODataApiManagementSettings
{
    /// <summary>
    /// Gets or sets the subscription key required by the API gateway.
    /// </summary>
    public string SubscriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the HTTP header used to transmit the subscription key.
    /// </summary>
    /// <value>The default value is <c>Ocp-Apim-Subscription-Key</c>, the Azure API Management standard header.</value>
    public string SubscriptionHeaderKey { get; set; } = "Ocp-Apim-Subscription-Key";

    /// <summary>
    /// Gets or sets additional HTTP headers to include with every request via the API gateway.
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();
}
