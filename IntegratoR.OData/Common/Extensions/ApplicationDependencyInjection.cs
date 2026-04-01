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
        // Register supporting services
        services.AddTransient<ODataAuthenticationHandler>();
        services.AddSingleton<ODataMetadataProvider>();

        // Configure HttpClient with Polly policies
        services.AddHttpClient("ODataClient")
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

            httpClient.Timeout = TimeSpan.FromSeconds(settings.Timeout);
            httpClient.BaseAddress = new Uri(settings.Url);

            var options = new ODataClientOptions
            {
                BaseUrl = settings.Url,
                HttpClient = httpClient
            };

            logger.LogInformation("OData client configured with base URL: {BaseUrl}", settings.Url);

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
