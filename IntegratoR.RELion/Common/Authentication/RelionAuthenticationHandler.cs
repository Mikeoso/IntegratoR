using System.Net;
using System.Net.Http.Headers;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Authentication;
using IntegratoR.RELion.Domain.Settings;
using Microsoft.Extensions.Options;

namespace IntegratoR.RELion.Common.Authentication;

/// <summary>
/// Injects authentication headers into HTTP requests for Relion API calls.
/// </summary>
public class RelionAuthenticationHandler : DelegatingHandler
{
    private readonly RelionSettings _settings;
    private readonly IAuthenticator _authenticator;

    public RelionAuthenticationHandler(IOptions<RelionSettings> settings, IAuthenticator authenticator)
    {
        _settings = settings.Value;
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_settings.AuthMode == RelionAuthMode.OAuth)
        {
            // Use the authenticator with Relion-specific settings
            var tokenResult = await _authenticator.GetAccessTokenAsync(_settings.ClientId, _settings.ClientSecret, _settings.TenantId, _settings.Resource).ConfigureAwait(false);
            if (tokenResult.IsSuccess)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);
            }
            else
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    ReasonPhrase = $"Failed to acquire Relion OAuth token: {tokenResult?.GetError()?.Message}"
                };
            }
        }
        else
        {
            request.Headers.Add(_settings.SubscriptionHeaderKey, _settings.SubscriptionKey);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
