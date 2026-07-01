using FluentResults;

namespace IntegratoR.Abstractions.Interfaces.Authentication;

/// <summary>
/// Defines a contract for acquiring OAuth 2.0 access tokens for secure communication with backend services such as D365 F&amp;O.
/// </summary>
public interface IAuthenticator
{
    /// <summary>
    /// Asynchronously acquires a valid OAuth 2.0 access token for a specified D365 F&amp;O resource.
    /// </summary>
    /// <param name="clientId">The client ID (application ID) of the Azure AD app registration.</param>
    /// <param name="clientSecret">The client secret of the Azure AD app registration.</param>
    /// <param name="tenantId">The Azure AD tenant ID where the application is registered.</param>
    /// <param name="resource">The URI of the target resource to which access is requested; for D365 F&amp;O this is the environment base URL without a trailing slash.</param>
    /// <returns>A <see cref="Result{TValue}"/> carrying the access token on success, or a structured error on failure.</returns>
    [Obsolete("Since v1.4.0; use the overload that accepts a CancellationToken.")]
    Task<Result<string>> GetAccessTokenAsync(string clientId, string clientSecret, string tenantId, string resource);

    /// <summary>
    /// Asynchronously acquires a valid OAuth 2.0 access token for a specified D365 F&amp;O resource.
    /// </summary>
    /// <param name="clientId">The client ID (application ID) of the Azure AD app registration.</param>
    /// <param name="clientSecret">The client secret of the Azure AD app registration.</param>
    /// <param name="tenantId">The Azure AD tenant ID where the application is registered.</param>
    /// <param name="resource">The URI of the target resource to which access is requested; for D365 F&amp;O this is the environment base URL without a trailing slash.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the token acquisition to complete.</param>
    /// <returns>A <see cref="Result{TValue}"/> carrying the access token on success, or a structured error on failure.</returns>
    Task<Result<string>> GetAccessTokenAsync(string clientId, string clientSecret, string tenantId, string resource, CancellationToken cancellationToken);
}
