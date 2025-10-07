using IntegratoR.RELion.Domain.Settings;

namespace IntegratoR.RELion.Domain.Settings;

/// <summary>
/// Represents the configuration settings required to connect to the Relion API.
/// </summary>
public class RelionSettings
{
    #region Base Settings
    /// <summary>
    /// The base URL of the Relion API endpoint.
    /// </summary>
    public required string Url { get; set; }
    /// <summary>
    /// The specified Timeout for the Relion service calls. Default is 120 seconds.
    /// </summary>
    public int Timeout { get; set; } = 120;
    /// <summary>
    /// The company identifier within Relion to target for API requests.
    /// </summary>
    public string Company { get; set; } = string.Empty;
    #endregion
    #region API Management Settings
    /// <summary>
    /// The authentication mode to use when connecting to the Relion API.
    /// </summary>
    public RelionAuthMode AuthMode { get; set; }
    /// <summary>
    /// Subscription Key for the Relion API when using ApiKey authentication mode.
    /// </summary>
    public string SubscriptionKey { get; set; } = string.Empty;
    /// <summary>
    /// Subscription Header Key for the Relion API when using ApiKey authentication mode.
    /// </summary>
    public string SubscriptionHeaderKey { get; set; } = string.Empty;
    #endregion
    #region OAuth Settings
    /// <summary>
    /// The Client ID used for the OAuth Service to get a access token for the specified OData service.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;
    /// <summary>
    /// The Client Secret used for the OAuth Service to get a access token for the specified OData service.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>
    /// The specified Tenant ID used for the OAuth Service to get a access token for the specified OData service.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;
    /// <summary>
    /// The specified Resource used for the OAuth Service to get a access token for the specified OData service.
    /// </summary>
    public string Resource { get; set; } = string.Empty;
    #endregion
}
