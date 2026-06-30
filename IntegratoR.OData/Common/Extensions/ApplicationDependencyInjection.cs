using System.Net;
using IntegratoR.OData.Common.Authentication;
using IntegratoR.OData.Common.Services;
using IntegratoR.OData.Domain.Settings;
using IntegratoR.OData.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PanoramicData.OData.Client;
using PanoramicData.OData.Client.Exceptions;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;

namespace IntegratoR.OData.Common.Extensions;

/// <summary>
/// Provides dependency injection configuration for OData infrastructure services.
/// </summary>
internal static class ApplicationDependencyInjection
{
    /// <summary>
    /// Registers OData client services with configuration from IConfiguration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration containing ODataSettings section.</param>
    public static IServiceCollection AddODataClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ODataSettings>(configuration.GetSection("ODataSettings"));
        services.AddODataDependencies();
        return services;
    }

    /// <summary>
    /// Registers OData client services with programmatic configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure OData settings.</param>
    public static IServiceCollection AddODataClient(
        this IServiceCollection services,
        Action<ODataSettings> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddODataDependencies();
        return services;
    }

    private static void AddODataDependencies(this IServiceCollection services)
    {
        // Fail fast on dangerous or incomplete ODataSettings (an auth header smuggled into
        // DefaultHeaders, or an authentication mode missing its credentials).
        services.AddSingleton<IValidateOptions<ODataSettings>, ODataSettingsValidator>();
        services.AddOptions<ODataSettings>().ValidateOnStart();

        // Register supporting services
        services.AddTransient<ODataAuthenticationHandler>();
        services.AddSingleton<ODataMetadataProvider>();

        // Configure HttpClient with Polly policies
        services.AddHttpClient("ODataClient", (serviceProvider, httpClient) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<ODataSettings>>().Value;

                httpClient.Timeout = TimeSpan.FromSeconds(settings.Timeout);

                // Normalise the configured URL: when a relative request URI does NOT start with '/',
                // Uri composition treats the final segment of BaseAddress as a "file" and drops it
                // unless BaseAddress ends with '/'. PanoramicData emits non-rooted relatives like
                // "LedgerJournalHeaders", so a configured URL of "https://host/fo" without a trailing
                // slash resolves to "https://host/LedgerJournalHeaders" — the "/fo" segment is lost.
                // (Note: rooted relatives like "/LedgerJournalHeaders" always replace the path
                // regardless of trailing slash, so those requests would still be broken. PanoramicData
                // does not emit rooted relatives for us, so the trailing-slash fix is sufficient here.)
                // We do NOT mutate settings.Url itself — IOptions<ODataSettings>.Value.Url continues
                // to reflect what the consumer wrote.
                httpClient.BaseAddress = new Uri(NormaliseBaseUrl(settings.Url));
            })
            .AddHttpMessageHandler<ODataAuthenticationHandler>()
            // Retry Policy - handles transient failures with exponential backoff
            .AddPolicyHandler((serviceProvider, request) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<ODataSettings>>().Value;

                if (!settings.Resilience.EnableRetries)
                {
                    return Policy.NoOpAsync<HttpResponseMessage>();
                }

                var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                var logger = loggerFactory?.CreateLogger("IntegratoR.OData.HttpRetry");

                return HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .Or<TaskCanceledException>()
                    .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests)
                    .WaitAndRetryAsync(
                        settings.Resilience.RetryCount,
                        retryAttempt => CalculateRetryDelay(retryAttempt),
                        onRetry: (outcome, timespan, retryCount, context) =>
                        {
                            logger?.LogWarning(
                                "HTTP retry attempt {RetryCount} after {DelayMs}ms. Reason: {Reason}",
                                retryCount,
                                timespan.TotalMilliseconds,
                                outcome.Exception?.Message ??
                                outcome.Result?.StatusCode.ToString() ?? "Unknown");
                        });
            })
            // Circuit Breaker Policy - prevents cascading failures
            .AddPolicyHandler((serviceProvider, request) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<ODataSettings>>().Value;

                if (!settings.Resilience.UseCircuitBreaker)
                {
                    return Policy.NoOpAsync<HttpResponseMessage>();
                }

                return HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .CircuitBreakerAsync(
                        handledEventsAllowedBeforeBreaking: settings.Resilience.CircuitBreakerThreshold,
                        durationOfBreak: TimeSpan.FromSeconds(settings.Resilience.CircuitBreakerDurationInSeconds));
            });

        // Register PanoramicData ODataClient
        services.AddSingleton(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<ODataSettings>>().Value;
            var logger = serviceProvider.GetRequiredService<ILogger<ODataClient>>();

            var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>()
                .CreateClient("ODataClient");

            // Timeout and BaseAddress are configured via AddHttpClient above. We only need to pass
            // the same normalised URL to ODataClientOptions.BaseUrl so PanoramicData's independent
            // URL composition stays in lockstep with HttpClient.BaseAddress.
            string normalisedUrl = NormaliseBaseUrl(settings.Url);

            var options = new ODataClientOptions
            {
                BaseUrl = normalisedUrl,
                HttpClient = httpClient
            };

            logger.LogInformation("OData client configured with base URL: {BaseUrl}", normalisedUrl);

            return new ODataClient(options);
        });

        // Register the adapter that wraps PanoramicData's ODataClient
        services.AddSingleton<IODataClientAdapter>(serviceProvider =>
        {
            var client = serviceProvider.GetRequiredService<ODataClient>();
            return new ODataClientAdapter(client);
        });

        // Register AsyncRetryPolicy for OData operations (in addition to HTTP retries)
        services.AddSingleton(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<ODataSettings>>().Value;

            if (!settings.Resilience.EnableRetries)
            {
                return Policy.Handle<Exception>(_ => false)
                    .WaitAndRetryAsync(0, _ => TimeSpan.Zero);
            }

            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger("IntegratoR.OData.Retry");

            return Policy
                .Handle<ODataClientException>(ex => IsTransientError(ex.StatusCode))
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    settings.Resilience.RetryCount,
                    retryAttempt => CalculateRetryDelay(retryAttempt),
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        logger?.LogWarning(
                            exception,
                            "OData operation retry attempt {RetryCount} after {DelayMs}ms",
                            retryCount,
                            timespan.TotalMilliseconds);
                    });
        });

        // Register repository services
        services.AddScoped(typeof(IntegratoR.Abstractions.Interfaces.Services.IService<>), typeof(ODataService<>));
        services.AddScoped(typeof(IODataService<>), typeof(ODataService<>));
        services.AddScoped(typeof(IODataBatchService<>), typeof(ODataService<>));
    }

    /// <summary>
    /// Appends a trailing slash to the configured OData base URL if absent.
    /// Required because <see cref="HttpClient.BaseAddress"/> silently drops preceding path
    /// segments when a relative request URI starts with '/' unless the base address ends with '/'.
    /// </summary>
    /// <remarks>
    /// Throws <see cref="ArgumentException"/> on null/whitespace so consumers get a clear error
    /// at DI resolution instead of a misleading <see cref="UriFormatException"/> from
    /// <see cref="Uri(string)"/>. Exposed as <c>internal</c> for regression tests in
    /// <c>IntegratoR.OData.Tests</c> (see <c>InternalsVisibleTo</c> in the csproj).
    /// </remarks>
    internal static string NormaliseBaseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException(
                "ODataSettings.Url must be set to a non-empty absolute URL before resolving the OData client. " +
                "Check your configuration (appsettings.json / environment variables).",
                nameof(url));
        }

        return url.TrimEnd('/') + "/";
    }

    /// <summary>
    /// Calculates retry delay with exponential backoff and jitter.
    /// </summary>
    private static TimeSpan CalculateRetryDelay(int retryAttempt)
    {
        var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
        var jitterMs = Random.Shared.Next(0, (int)(baseDelay.TotalMilliseconds * 0.25));
        return baseDelay + TimeSpan.FromMilliseconds(jitterMs);
    }

    /// <summary>
    /// Determines if an HTTP status code represents a transient error worth retrying.
    /// </summary>
    private static bool IsTransientError(int? statusCode)
    {
        return statusCode switch
        {
            408 => true, // RequestTimeout
            429 => true, // TooManyRequests
            >= 500 => true, // All server errors (500, 502, 503, 504, etc.)
            _ => false
        };
    }
}
