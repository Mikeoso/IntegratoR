using IntegratoR.Abstractions.Common.Results;

namespace IntegratoR.Abstractions.Interfaces.Authentication;

/// <summary>
/// Defines a contract for acquiring OAuth 2.0 access tokens for secure communication with backend services like Dynamics 365 F&O.
/// </summary>
/// <remarks>
/// This interface abstracts the complexities of the OAuth 2.0 client credentials grant flow, which is the
/// standard mechanism for service-to-service authentication with Azure AD-protected resources.
///
/// A typical implementation of this interface will use the Microsoft Authentication Library (MSAL)
/// to handle the token acquisition, caching, and renewal process. Abstracting this functionality
/// is a best practice that decouples business logic from the specific authentication library and
/// greatly simplifies unit testing by allowing this interface to be mocked.
/// </remarks>
public interface IAuthenticator
{
    /// <summary>
    /// Asynchronously acquires a valid OAuth 2.0 access token for a specified D365 F&O resource.
    /// </summary>
    /// <param name="clientId">The Client ID (or Application ID) of the Azure AD App Registration.</param>
    /// <param name="clientSecret">The Client Secret of the Azure AD App Registration.</param>
    /// <param name="tenantId">The Azure AD Tenant ID where the application is registered.</param>
    /// <param name="resource">The URI of the target resource/API to which access is being requested.</param>
    /// <remarks>
    /// For D365 F&O, the <paramref name="resource"/> value is the base URL of the F&O environment,
    /// for example: `https://your-environment-name.operations.dynamics.com`. Do not append a trailing slash.
    /// </remarks>
    /// <returns>
    /// A <see cref="Task"/> that resolves to a <see cref="Result{TValue}"/>. On success, the result
    /// contains the access token string, ready to be used in an HTTP Authorization header (e.g., "Bearer {token}").
    /// On failure, it contains a structured <see cref="Error"/> detailing the reason for the authentication failure.
    /// </returns>
    Task<Result<string>> GetAccessTokenAsync(string clientId, string clientSecret, string tenantId, string resource);
}