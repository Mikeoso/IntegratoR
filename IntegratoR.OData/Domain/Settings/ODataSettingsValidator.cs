using Microsoft.Extensions.Options;

namespace IntegratoR.OData.Domain.Settings;

/// <summary>
/// Validates <see cref="ODataSettings"/> at startup and on first use, failing fast on dangerous or incomplete configuration.
/// </summary>
/// <remarks>
/// Rejects an authentication header carried in <see cref="ODataApiManagementSettings.DefaultHeaders"/> (the framework owns the auth header) and an authentication mode without its required credentials.
/// </remarks>
public sealed class ODataSettingsValidator : IValidateOptions<ODataSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ODataSettings options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        // (a) DefaultHeaders must never carry an authentication header — those are set by the
        //     framework. Compared case-insensitively so 'authorization' cannot slip past 'Authorization'.
        var forbiddenHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization",
            "Bearer",
            "Ocp-Apim-Subscription-Key",
            options.Authentication.ApiManagement.SubscriptionHeaderKey,
        };

        foreach (string headerKey in options.Authentication.ApiManagement.DefaultHeaders.Keys)
        {
            if (forbiddenHeaders.Contains(headerKey))
            {
                failures.Add(
                    $"ODataSettings.Authentication.ApiManagement.DefaultHeaders must not contain the authentication header '{headerKey}'. " +
                    "Authentication headers are applied by the framework, not via DefaultHeaders.");
            }
        }

        // (b)/(c) The selected authentication mode must have its credentials populated.
        switch (options.Authentication.Mode)
        {
            case AuthenticationMode.ApiKey:
                if (string.IsNullOrWhiteSpace(options.Authentication.ApiManagement.SubscriptionKey))
                {
                    failures.Add("ODataSettings.Authentication.ApiManagement.SubscriptionKey must be set when AuthenticationMode is ApiKey.");
                }

                if (string.IsNullOrWhiteSpace(options.Authentication.ApiManagement.SubscriptionHeaderKey))
                {
                    failures.Add("ODataSettings.Authentication.ApiManagement.SubscriptionHeaderKey must be set when AuthenticationMode is ApiKey (it names the HTTP header that carries the subscription key).");
                }

                break;

            case AuthenticationMode.OAuth:
                ODataOAuthSettings oauth = options.Authentication.OAuth;
                if (string.IsNullOrWhiteSpace(oauth.ClientId))
                {
                    failures.Add("ODataSettings.Authentication.OAuth.ClientId must be set when AuthenticationMode is OAuth.");
                }

                if (string.IsNullOrWhiteSpace(oauth.ClientSecret))
                {
                    failures.Add("ODataSettings.Authentication.OAuth.ClientSecret must be set when AuthenticationMode is OAuth.");
                }

                if (string.IsNullOrWhiteSpace(oauth.TenantId))
                {
                    failures.Add("ODataSettings.Authentication.OAuth.TenantId must be set when AuthenticationMode is OAuth.");
                }

                if (string.IsNullOrWhiteSpace(oauth.Resource))
                {
                    failures.Add("ODataSettings.Authentication.OAuth.Resource must be set when AuthenticationMode is OAuth.");
                }

                break;

            default:
                // An out-of-range enum value (e.g. from a typo'd config binding) must not pass
                // silently — the auth handler would otherwise fall through to the ApiKey/APIM path
                // with no credentials. Fail fast and force an explicit, recognised mode.
                failures.Add(
                    $"ODataSettings.Authentication.Mode has an unrecognised value '{options.Authentication.Mode}'. " +
                    "Set it explicitly to ApiKey or OAuth.");
                break;
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
