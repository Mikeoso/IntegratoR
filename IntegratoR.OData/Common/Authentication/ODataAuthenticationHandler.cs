using System.Net;
using System.Net.Http.Headers;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Authentication;
using IntegratoR.OData.Domain.Settings;
using Microsoft.Extensions.Options;

namespace IntegratoR.OData.Common.Authentication;

/// <summary>
/// Provides an <see cref="HttpClient"/> message handler that acquires and attaches the appropriate
/// authentication header to outgoing requests destined for D365 F&amp;O OData endpoints.
/// </summary>
/// <remarks>
/// Supports two modes based on the provided <see cref="ODataSettings"/>: OAuth (a Bearer token acquired
/// via the injected <see cref="IAuthenticator"/> using the client credentials flow) and subscription
/// key (a custom header for requests routed through Azure API Management). If OAuth token acquisition
/// fails, the handler short-circuits and returns a 401 Unauthorized response without calling downstream.
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
        if (_settings.Authentication.Mode == AuthenticationMode.OAuth)
        {
            Result<string> tokenResult = await _authenticator.GetAccessTokenAsync(_settings.Authentication.OAuth.ClientId, _settings.Authentication.OAuth.ClientSecret, _settings.Authentication.OAuth.TenantId, _settings.Authentication.OAuth.Resource, cancellationToken).ConfigureAwait(false);

            if (tokenResult.IsSuccess)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);
            }
            else
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    ReasonPhrase = "Authentication failed"
                };
            }
        }
        else
        {
            request.Headers.Add(_settings.Authentication.ApiManagement.SubscriptionHeaderKey, _settings.Authentication.ApiManagement.SubscriptionKey);

            foreach (var header in _settings.Authentication.ApiManagement.DefaultHeaders)
            {
                request.Headers.Add(header.Key, header.Value);
            }
        }
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
