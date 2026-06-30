using FluentAssertions;
using IntegratoR.OData.Domain.Settings;
using Xunit;

namespace IntegratoR.OData.Tests.Domain.Settings;

/// <summary>
/// Tests for <see cref="ODataSettingsValidator"/>, which fails fast on dangerous or incomplete
/// <see cref="ODataSettings"/> — an authentication header smuggled into DefaultHeaders, or an
/// authentication mode missing its required credentials.
/// </summary>
public class ODataSettingsValidatorTests
{
    private readonly ODataSettingsValidator _sut = new();

    /// <summary>
    /// A forbidden authentication header in DefaultHeaders must fail validation, compared
    /// case-insensitively and including a custom configured SubscriptionHeaderKey.
    /// </summary>
    [Theory]
    [InlineData("Authorization", "Ocp-Apim-Subscription-Key")]
    [InlineData("authorization", "Ocp-Apim-Subscription-Key")]
    [InlineData("Bearer", "Ocp-Apim-Subscription-Key")]
    [InlineData("Ocp-Apim-Subscription-Key", "Ocp-Apim-Subscription-Key")]
    [InlineData("x-my-key", "X-My-Key")]
    public void Validate_DefaultHeadersContainsForbiddenKey_Fails(string forbiddenHeader, string subscriptionHeaderKey)
    {
        // Arrange -- valid ApiKey base config, then poison DefaultHeaders with a forbidden header.
        var settings = new ODataSettings
        {
            Url = "https://test.operations.dynamics.com/data",
            Authentication = new ODataAuthenticationSettings
            {
                Mode = AuthenticationMode.ApiKey,
                ApiManagement = new ODataApiManagementSettings
                {
                    SubscriptionKey = "valid-key",
                    SubscriptionHeaderKey = subscriptionHeaderKey,
                    DefaultHeaders = new Dictionary<string, string> { [forbiddenHeader] = "value" }
                }
            }
        };

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
    }

    /// <summary>
    /// A benign custom header in DefaultHeaders must pass the forbidden-header rule.
    /// </summary>
    [Fact]
    public void Validate_DefaultHeadersContainsBenignKey_PassesForbiddenHeaderRule()
    {
        // Arrange
        var settings = new ODataSettings
        {
            Url = "https://test.operations.dynamics.com/data",
            Authentication = new ODataAuthenticationSettings
            {
                Mode = AuthenticationMode.ApiKey,
                ApiManagement = new ODataApiManagementSettings
                {
                    SubscriptionKey = "valid-key",
                    SubscriptionHeaderKey = "Ocp-Apim-Subscription-Key",
                    DefaultHeaders = new Dictionary<string, string> { ["d365foenvironment"] = "UAT" }
                }
            }
        };

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_ApiKeyModeWithBlankSubscriptionKey_Fails()
    {
        // Arrange
        var settings = new ODataSettings
        {
            Url = "https://test.operations.dynamics.com/data",
            Authentication = new ODataAuthenticationSettings
            {
                Mode = AuthenticationMode.ApiKey,
                ApiManagement = new ODataApiManagementSettings { SubscriptionKey = string.Empty }
            }
        };

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
    }

    /// <summary>
    /// In OAuth mode, each of the four required credentials being blank must fail validation.
    /// </summary>
    [Theory]
    [InlineData("", "secret", "tenant", "https://resource")]
    [InlineData("client", "", "tenant", "https://resource")]
    [InlineData("client", "secret", "", "https://resource")]
    [InlineData("client", "secret", "tenant", "")]
    public void Validate_OAuthModeWithBlankCredential_Fails(string clientId, string clientSecret, string tenantId, string resource)
    {
        // Arrange
        var settings = new ODataSettings
        {
            Url = "https://test.operations.dynamics.com/data",
            Authentication = new ODataAuthenticationSettings
            {
                Mode = AuthenticationMode.OAuth,
                OAuth = new ODataOAuthSettings
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret,
                    TenantId = tenantId,
                    Resource = resource
                }
            }
        };

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidApiKeyConfig_Succeeds()
    {
        // Arrange
        var settings = new ODataSettings
        {
            Url = "https://test.operations.dynamics.com/data",
            Authentication = new ODataAuthenticationSettings
            {
                Mode = AuthenticationMode.ApiKey,
                ApiManagement = new ODataApiManagementSettings
                {
                    SubscriptionKey = "valid-subscription-key",
                    SubscriptionHeaderKey = "Ocp-Apim-Subscription-Key"
                }
            }
        };

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidOAuthConfig_Succeeds()
    {
        // Arrange
        var settings = new ODataSettings
        {
            Url = "https://test.operations.dynamics.com/data",
            Authentication = new ODataAuthenticationSettings
            {
                Mode = AuthenticationMode.OAuth,
                OAuth = new ODataOAuthSettings
                {
                    ClientId = "client",
                    ClientSecret = "secret",
                    TenantId = "tenant",
                    Resource = "https://resource"
                }
            }
        };

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    /// <summary>
    /// An out-of-range <see cref="AuthenticationMode"/> (e.g. a typo'd config binding) must fail
    /// fast rather than fall through to the credential-less APIM path.
    /// </summary>
    [Fact]
    public void Validate_UnrecognisedAuthenticationMode_Fails()
    {
        // Arrange
        var settings = new ODataSettings
        {
            Url = "https://test.operations.dynamics.com/data",
            Authentication = new ODataAuthenticationSettings
            {
                Mode = (AuthenticationMode)999,
                ApiManagement = new ODataApiManagementSettings
                {
                    SubscriptionKey = "valid-key",
                    SubscriptionHeaderKey = "Ocp-Apim-Subscription-Key"
                }
            }
        };

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
    }
}
