using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using FluentResults;
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
/// acquiring a new token from Azure AD if necessary, and caching it for future use. The built
/// <see cref="IConfidentialClientApplication"/> is reused per (clientId, tenantId, resource) so
/// MSAL's own in-memory token cache is consulted before a network call is made.
///
/// **Important Architectural Note:** the proactive-expiry token cache uses <see cref="IMemoryCache"/>,
/// which is local to a single instance. For scaled-out, multi-instance environments an
/// <c>IDistributedCache</c>-backed implementation should be used instead.
/// </remarks>
public class OAuthAuthenticator : IAuthenticator
{
    // Reuse the built confidential-client app per (clientId, tenantId, resource) so MSAL's internal
    // token cache is reused instead of being discarded on every IMemoryCache miss.
    private static readonly ConcurrentDictionary<string, IConfidentialClientApplication> AppCache = new();

    private readonly IMemoryCache _memoryCache;
    private readonly Func<string, string, string, string, IConfidentialClientApplication> _appFactory;
    private readonly Func<IConfidentialClientApplication, string[], CancellationToken, Task<AcquiredToken>> _tokenAcquirer;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthAuthenticator"/> class.
    /// </summary>
    /// <param name="memoryCache">The memory cache instance, injected via dependency injection.</param>
    public OAuthAuthenticator(IMemoryCache memoryCache)
        : this(memoryCache, DefaultAppFactory, DefaultAcquireAsync)
    {
    }

    /// <summary>
    /// Test seam constructor. Lets unit tests substitute the confidential-client app factory and the
    /// token-acquisition delegate so the MSAL network path can be exercised without a real Azure AD.
    /// </summary>
    internal OAuthAuthenticator(
        IMemoryCache memoryCache,
        Func<string, string, string, string, IConfidentialClientApplication> appFactory,
        Func<IConfidentialClientApplication, string[], CancellationToken, Task<AcquiredToken>> tokenAcquirer)
    {
        _memoryCache = memoryCache;
        _appFactory = appFactory;
        _tokenAcquirer = tokenAcquirer;
    }

    /// <inheritdoc />
    [Obsolete("Since v1.4.0; use the overload that accepts a CancellationToken.")]
    public Task<Result<string>> GetAccessTokenAsync(string clientId, string clientSecret, string tenantId, string resource)
        => GetAccessTokenAsync(clientId, clientSecret, tenantId, resource, CancellationToken.None);

    /// <inheritdoc />
    /// <remarks>
    /// Attempts to serve a cached token first; on a miss it acquires a new token via the reused
    /// confidential-client app using the <c>/.default</c> scope, then caches it with a 5-minute
    /// proactive-expiry buffer. All MSAL failures (service and client) are mapped to a failed
    /// <see cref="Result{TValue}"/> rather than thrown.
    /// </remarks>
    public async Task<Result<string>> GetAccessTokenAsync(string clientId, string clientSecret, string tenantId, string resource, CancellationToken cancellationToken)
    {
        // A unique cache key is generated based on the client and resource to ensure
        // tokens for different applications or environments do not collide.
        var tokenCacheKey = $"AccessToken-{clientId}-{resource}";

        if (_memoryCache.TryGetValue(tokenCacheKey, out string? cachedToken))
        {
            return Result.Ok(cachedToken!);
        }

        try
        {
            // Include a hash of the secret in the cache key so a rotated client secret yields a fresh
            // app (with a fresh MSAL token cache) instead of silently reusing a stale one. The raw
            // secret is never placed in the key.
            var secretHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clientSecret)));
            var appKey = $"{clientId}|{tenantId}|{resource}|{secretHash}";
            IConfidentialClientApplication confidentialClientApp =
                AppCache.GetOrAdd(appKey, _ => _appFactory(clientId, clientSecret, tenantId, resource));

            // The "/.default" scope requests all application-level permissions granted to this
            // application registration for the specified resource.
            var scopes = new[] { $"{resource}/.default" };
            AcquiredToken token = await _tokenAcquirer(confidentialClientApp, scopes, cancellationToken).ConfigureAwait(false);

            // Proactively expire the cache entry 5 minutes before the actual token expires to avoid
            // using an invalidated token due to clock skew or transit delays.
            var cacheExpiration = token.ExpiresOn.Subtract(TimeSpan.FromMinutes(5));
            _memoryCache.Set(tokenCacheKey, token.AccessToken, cacheExpiration);

            return Result.Ok(token.AccessToken);
        }
        catch (MsalException ex)
        {
            // Catch the MSAL BASE type so both MsalServiceException and MsalClientException map to a
            // structured failed Result (never thrown). The short MSAL error code (e.g. "invalid_client")
            // is safe to surface; the full ex.Message can carry AADSTS codes / tenant IDs, so it is kept
            // only on the inner exception for server-side diagnostics, never in the error message.
            var code = string.IsNullOrEmpty(ex.ErrorCode) ? "Unknown" : ex.ErrorCode;
            return Result.Fail<string>(new IntegrationError($"Auth.Msal.{code}", "Token acquisition failed", ErrorType.Failure, ex));
        }
    }

    private static IConfidentialClientApplication DefaultAppFactory(string clientId, string clientSecret, string tenantId, string resource)
        => ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithClientSecret(clientSecret)
            .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
            .Build();

    private static async Task<AcquiredToken> DefaultAcquireAsync(IConfidentialClientApplication app, string[] scopes, CancellationToken cancellationToken)
    {
        AuthenticationResult result = await app.AcquireTokenForClient(scopes).ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return new AcquiredToken(result.AccessToken, result.ExpiresOn);
    }
}

/// <summary>
/// Minimal token-acquisition result used by the <see cref="OAuthAuthenticator"/> test seam so the
/// acquisition path can be substituted without constructing an MSAL <see cref="AuthenticationResult"/>.
/// </summary>
internal sealed record AcquiredToken(string AccessToken, DateTimeOffset ExpiresOn);
