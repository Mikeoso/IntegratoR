namespace IntegratoR.OData.Domain.Settings;

/// <summary>
/// OAuth 2.0 client credentials settings for authenticating with D365 F&amp;O.
/// </summary>
public class ODataOAuthSettings
{
    /// <summary>
    /// Gets or sets the Client ID (Application ID) from the Azure AD App Registration.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Client Secret for the service principal.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure AD Tenant ID where the application is registered.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the App ID URI of the D365 F&amp;O resource to which access is being requested.
    /// </summary>
    public string Resource { get; set; } = string.Empty;
}
