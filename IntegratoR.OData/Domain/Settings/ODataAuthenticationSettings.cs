namespace IntegratoR.OData.Domain.Settings;

/// <summary>
/// Represents the authentication settings for the OData client, supporting both OAuth 2.0 and API Management gateway modes.
/// </summary>
public class ODataAuthenticationSettings
{
    /// <summary>
    /// Gets or sets the authentication mode to use for the connection.
    /// </summary>
    /// <value>The default value is <see cref="AuthenticationMode.ApiKey"/>.</value>
    public AuthenticationMode Mode { get; set; } = AuthenticationMode.ApiKey;

    /// <summary>
    /// Gets or sets the OAuth 2.0 client credentials settings.
    /// Used when <see cref="Mode"/> is <see cref="AuthenticationMode.OAuth"/>.
    /// </summary>
    public ODataOAuthSettings OAuth { get; set; } = new();

    /// <summary>
    /// Gets or sets the API Management gateway settings.
    /// Used when <see cref="Mode"/> is <see cref="AuthenticationMode.ApiKey"/>.
    /// </summary>
    public ODataApiManagementSettings ApiManagement { get; set; } = new();
}
