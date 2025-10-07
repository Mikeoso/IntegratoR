using IntegratoR.Abstractions.Common.Result;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Identity.Client;

namespace IntegratoR.Application.Common.Authentication;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file contains a concrete implementation of the IAuthenticator interface using the
// Microsoft Authentication Library (MSAL) for .NET. It is designed to handle the OAuth 2.0
// client credentials grant flow, which is the standard method for service-to-service
// authentication with Azure AD-protected resources like Dynamics 365 F&O.
// </remarks>
// ---------------------------------------------------------------------------------------------

/// <summary>
/// An authenticator that acquires OAuth 2.0 access tokens from Azure Active Directory
/// using the MSAL library and provides in-memory caching to optimize performance.
/// </summary>
/// <remarks>
/// This implementation is responsible for the entire token lifecycle: checking the cache,
/// acquiring a new token from Azure AD if necessary, and caching it for future use.
///
/// **Important Architectural Note:** This implementation uses <see cref="IMemoryCache"/>,
/// which is local to a single server instance. While suitable for single-instance applications
/// or development environments, it is **not appropriate** for stateless, multi-instance environments
/// like Azure Functions on a Consumption Plan. In a scaled-out scenario, each function instance
/// would have its own separate cache, leading to unnecessary token requests. For such environments,
/// an implementation using <c>IDistributedCache</c> (backed by a service like Azure Cache for Redis)
/// should be used instead to share the token across all instances.
/// </remarks>
public class OAuthAuthenticator : IAuthenticator
{
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthAuthenticator"/> class.
    /// </summary>
    /// <param name="memoryCache">The memory cache instance, injected via dependency injection.</param>
    public OAuthAuthenticator(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method first attempts to retrieve a valid token from the in-memory cache. If a token is
    /// not found, it uses the MSAL <see cref="ConfidentialClientApplicationBuilder"/> to construct a
    /// request to Azure AD.
    ///
    /// It uses the modern `/.default` scope, which is the recommended approach for the client
    /// credentials flow, requesting all statically-defined application permissions for the given resource.
    ///
    /// Upon successful acquisition, the token is cached with a proactive expiration buffer of 5 minutes.
    /// This is a crucial best practice to prevent race conditions and clock skew issues where the
    /// application might attempt to use a token just as it expires.
    /// </remarks>
    public async Task<Result<string>> GetAccessTokenAsync(string clientId, string clientSecret, string tenantId, string resource)
    {
        // A unique cache key is generated based on the client and resource to ensure
        // tokens for different applications or environments do not collide.
        var tokenCacheKey = $"AccessToken-{clientId}-{resource}";

        if (_memoryCache.TryGetValue(tokenCacheKey, out string? cachedToken))
        {
            return Result<string>.Ok(cachedToken!);
        }

        try
        {
            var confidentialClientApp = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
                .Build();

            // The "/.default" scope requests all application-level permissions that have been
            // granted to this application registration for the specified resource.
            var scopes = new[] { $"{resource}/.default" };
            var authResult = await confidentialClientApp.AcquireTokenForClient(scopes).ExecuteAsync();

            // Proactively expire the cache entry 5 minutes before the actual token expires
            // to avoid using an invalidated token due to clock skew or transit delays.
            var cacheExpiration = authResult.ExpiresOn.Subtract(TimeSpan.FromMinutes(5));
            _memoryCache.Set(tokenCacheKey, authResult.AccessToken, cacheExpiration);

            return Result<string>.Ok(authResult.AccessToken);
        }
        catch (MsalServiceException ex)
        {
            // Catching the specific MSAL exception allows us to create a rich, structured error
            // that is agnostic of the underlying library, providing a stable error contract.
            // The MSAL error code is included for easier debugging.
            return Result<string>.Fail(new Error($"Auth.Msal.{ex.ErrorCode}", ex.Message, ErrorType.Failure, ex));
        }
    }
}