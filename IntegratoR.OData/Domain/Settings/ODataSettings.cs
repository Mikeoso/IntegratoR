// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file defines a strongly-typed configuration class for all OData connection settings.
// This class is designed to be used with the .NET IOptions pattern, allowing for a clean,
// configurable, and testable way to manage application settings.
// </remarks>
// ---------------------------------------------------------------------------------------------

using IntegratoR.OData.Domain.Settings;

namespace IntegratoR.OData.Domain.Settings;

/// <summary>
/// Encapsulates all configuration settings required to connect and authenticate with a
/// D365 F&O OData endpoint.
/// </summary>
/// <remarks>
/// An instance of this class is typically populated from the `appsettings.json` file
/// during application startup and injected into services via `IOptions&lt;ODataSettings&gt;`.
/// </remarks>
public class ODataSettings
{
    #region General Connection Settings

    /// <summary>
    /// Gets or sets the base URL of the D365 F&O OData endpoint.
    /// </summary>
    /// <example>https://your-environment.operations.dynamics.com/data</example>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timeout in seconds for individual HTTP requests to the OData service.
    /// </summary>
    public double Timeout { get; set; } = 120;

    /// <summary>
    /// Gets or sets the authentication mode to use for the connection.
    /// </summary>
    /// <seealso cref="ODataAuthMode"/>
    public ODataAuthMode AuthMode { get; set; }

    /// <summary>
    /// Represents additional HTTP headers to include with every request to the OData service.
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();
    #endregion

    #region OAuth 2.0 Settings

    // <remarks>These settings are required only when AuthMode is set to ODataAuthMode.OAuth.</remarks>

    /// <summary>
    /// Gets or sets the Client ID (Application ID) for the service principal.
    /// </summary>
    /// <remarks>
    /// This value is obtained from the Azure AD App Registration that has been granted
    /// permissions to access Dynamics 365 F&O.
    /// </remarks>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Client Secret for the service principal.
    /// </summary>
    /// <remarks>
    /// This is the secret value generated for the Azure AD App Registration. It should be
    /// stored securely, for example in Azure Key Vault.
    /// </remarks>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure AD Tenant ID where the application is registered.
    /// </summary>
    /// <remarks>This is the Directory (tenant) ID from your Azure Active Directory.</remarks>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the App ID URI of the D365 F&O resource to which access is being requested.
    /// </summary>
    /// <remarks>
    /// For a standard D365 F&O environment, this value is the same as the base URL
    /// of the environment (e.g., https://your-environment.operations.dynamics.com).
    /// </remarks>
    public string Resource { get; set; } = string.Empty;

    #endregion

    #region API Management (Gateway) Settings

    // <remarks>These settings are required only when AuthMode is set to ODataAuthMode.ApiKey.</remarks>

    /// <summary>
    /// Gets or sets the subscription key required by an API gateway (e.g., Azure API Management).
    /// </summary>
    public string SubscriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the HTTP header used to transmit the subscription key.
    /// </summary>
    /// <remarks>
    /// The default value, `Ocp-Apim-Subscription-Key`, is the standard header used by Azure API Management.
    /// </remarks>
    public string SubscriptionHeaderKey { get; set; } = "Ocp-Apim-Subscription-Key";

    #endregion
}