namespace IntegratoR.OData.Domain.Settings;

/// <summary>
/// Specifies the authentication method used by the OData client when communicating with an OData endpoint.
/// </summary>
public enum AuthenticationMode
{
    /// <summary>
    /// Authentication using a static subscription key via an API gateway such as Azure API Management.
    /// </summary>
    ApiKey,

    /// <summary>
    /// Authentication using the OAuth 2.0 client credentials flow for direct service-to-service communication with D365 F&amp;O.
    /// </summary>
    OAuth
}
