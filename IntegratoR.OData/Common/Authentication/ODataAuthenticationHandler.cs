using IntegratoR.Abstractions.Interfaces.Authentication;
using IntegratoR.OData.Domain.Settings;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file defines an HttpClient DelegatingHandler, which acts as middleware in the HTTP
// request pipeline. This pattern is a clean and powerful way to implement cross-cutting
// concerns like authentication, ensuring that every outgoing request is properly authenticated
// without cluttering the data access logic.
// </remarks>
// ---------------------------------------------------------------------------------------------
namespace IntegratoR.OData.Common.Authentication;

/// <summary>
/// An HttpClient message handler that automatically acquires and attaches the necessary
/// authentication headers to outgoing requests destined for D365 F&O OData endpoints.
/// </summary>
/// <remarks>
/// This handler is designed to be registered with an <c>IHttpClientFactory</c> when configuring
/// the typed HttpClient used by the OData client (e.g., Simple.OData.Client). Once registered,
/// it transparently handles authentication for every request.
///
/// It supports two primary authentication modes based on the provided <see cref="ODataSettings"/>:
/// <list type="bullet">
///   <item>
///     <term>OAuth</term>
///     <description>Used for direct communication with D365 F&O. It utilizes the injected
///     <see cref="IAuthenticator"/> to acquire a Bearer token via the client credentials flow.</description>
///   </item>
///   <item>
///     <term>Subscription Key</term>
///     <description>Used when requests are routed through a gateway like Azure API Management (APIM),
///     which often requires a subscription key in a custom header.</description>
///   </item>
/// </list>
/// If OAuth token acquisition fails, this handler will short-circuit the request and return an
/// <c>HttpResponseMessage</c> with status 401 Unauthorized.
/// </remarks>
public class ODataAuthenticationHandler : DelegatingHandler
{
    private readonly ODataSettings _settings;
    private readonly IAuthenticator _authenticator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="settings">The OData configuration settings, injected via <see cref="IOptions{TOptions}"/>.</param>
    /// <param name="authenticator">The service responsible for acquiring OAuth tokens.</param>
    public ODataAuthenticationHandler(IOptions<ODataSettings> settings, IAuthenticator authenticator)
    {
        _settings = settings.Value;
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
    }

    /// <summary>
    /// Intercepts an outgoing HTTP request to apply the appropriate authentication header
    /// before passing it to the next handler in the pipeline.
    /// </summary>
    /// <param name="request">The HTTP request message to be sent.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the
    /// <see cref="HttpResponseMessage"/> from the downstream server, or an immediate
    /// 401 Unauthorized response if OAuth token acquisition fails.
    /// </returns>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_settings.AuthMode == ODataAuthMode.OAuth)
        {
            var tokenResult = await _authenticator.GetAccessTokenAsync(_settings.ClientId, _settings.ClientSecret, _settings.TenantId, _settings.Resource);

            if (tokenResult.IsSuccess)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);
            }
            else
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    ReasonPhrase = $"Failed to acquire F&O OAuth token: {tokenResult.Error?.Message}"
                };
            }
        }
        else
        {
            request.Headers.Add(_settings.SubscriptionHeaderKey, _settings.SubscriptionKey);

            foreach (var header in _settings.DefaultHeaders)
            {
                request.Headers.Add(header.Key, header.Value);
            }
        }
        return await base.SendAsync(request, cancellationToken);
    }
}