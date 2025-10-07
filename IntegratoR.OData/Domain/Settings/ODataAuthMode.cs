// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file defines a configuration enum that specifies the authentication strategy for the
// OData client, allowing for flexible and scenario-specific authentication setups.
// </remarks>
// ---------------------------------------------------------------------------------------------

namespace IntegratoR.OData.Domain.Settings
{
    /// <summary>
    /// Specifies the authentication method to be used by the <see cref="ODataAuthenticationHandler"/>
    /// when communicating with an OData endpoint.
    /// </summary>
    public enum ODataAuthMode
    {
        /// <summary>
        /// Indicates that authentication will be performed using a static API key or subscription key.
        /// </summary>
        /// <remarks>
        /// This mode is typically used when the D365 F&O OData endpoint is exposed through an
        /// API gateway like **Azure API Management (APIM)**. The gateway requires a subscription
        //  key to be passed in a specific HTTP header for authorization and metering.
        /// </remarks>
        ApiKey,

        /// <summary>
        /// Indicates that authentication will be performed using the OAuth 2.0 client credentials flow.
        /// </summary>
        /// <remarks>
        /// This is the **standard and recommended** method for direct, secure, service-to-service
        /// communication with D365 F&O. It involves acquiring a Bearer token from Azure Active Directory
        /// to authorize requests.
        /// </remarks>
        OAuth
    }
}