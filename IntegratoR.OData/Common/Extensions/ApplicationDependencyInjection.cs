using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.Common.Authentication;
using IntegratoR.OData.Common.Services;
using IntegratoR.OData.Domain.Settings;
using IntegratoR.OData.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;
using Simple.OData.Client;
using System.Net;

namespace IntegratoR.OData.Common.Extensions;

/// <summary>
/// Provides dependency injection configuration for OData infrastructure services.
/// </summary>
public static class ApplicationDependencyInjection
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
        AppContext.SetSwitch("Switch.System.Xml.AllowDefaultResolver", true);

        services.AddTransient<ODataAuthenticationHandler>();
        services.AddTransient<ODataMetadataProvider>();

        services.AddHttpClient("ODataClient")
            .AddHttpMessageHandler<ODataAuthenticationHandler>()
            .AddPolicyHandler((serviceProvider, request) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<ODataSettings>>().Value;

                if (!settings.EnableRetries)
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
                        settings.RetryCount,
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
            .AddPolicyHandler((serviceProvider, request) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<ODataSettings>>().Value;

                if (!settings.UseCircuitBreaker)
                {
                    return Policy.NoOpAsync<HttpResponseMessage>();
                }

                return HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .CircuitBreakerAsync(
                        handledEventsAllowedBeforeBreaking: settings.CircuitBreakerThreshold,
                        durationOfBreak: TimeSpan.FromSeconds(settings.CircuitBreakerDurationSeconds));
            });

        services.AddSingleton<IODataClient>(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<ODataSettings>>().Value;
            var logger = serviceProvider.GetRequiredService<ILogger<IODataClient>>();

            var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>()
                .CreateClient("ODataClient");

            httpClient.Timeout = TimeSpan.FromSeconds(settings.Timeout);

            var odataClientSettings = new ODataClientSettings(httpClient)
            {
                BaseUri = new Uri(settings.Url),
                RequestTimeout = TimeSpan.FromSeconds(settings.Timeout),
                ReadUntypedAsString = true,
                IgnoreUnmappedProperties = true,
                PayloadFormat = ODataPayloadFormat.Json
            };

            // Load local metadata if configured
            if (!string.IsNullOrEmpty(settings.MetadataFilePath))
            {
                try
                {
                    var metadataProvider = serviceProvider.GetRequiredService<ODataMetadataProvider>();
                    var metadataXml = metadataProvider.LoadMetadata(settings.MetadataFilePath);

                    // Set metadata as string - Simple.OData.Client will parse it
                    odataClientSettings.MetadataDocument = metadataXml;

                    logger.LogInformation(
                        "OData client configured with local metadata from: {MetadataFilePath}",
                        settings.MetadataFilePath);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to load local metadata file from {MetadataFilePath}. Falling back to server metadata.",
                        settings.MetadataFilePath);
                    // Don't set MetadataDocument - let it fetch from server
                }
            }
            else
            {
                logger.LogInformation("OData client will fetch metadata from server on first request.");
            }

            return new ODataClient(odataClientSettings);
        });

        // Register AsyncRetryPolicy as optional service
        services.AddSingleton(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<ODataSettings>>().Value;

            if (!settings.EnableRetries)
            {
                return (AsyncRetryPolicy)null!;
            }

            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger("IntegratoR.OData.Retry");

            return Policy
                .Handle<WebRequestException>(ex => IsTransientError(ex.Code))
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    settings.RetryCount,
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

        services.AddScoped(typeof(IService<>), typeof(ODataService<>));
        services.AddScoped(typeof(IODataService<>), typeof(ODataService<>));
        services.AddScoped(typeof(IODataBatchService<>), typeof(ODataService<>));
    }

    private static TimeSpan CalculateRetryDelay(int retryAttempt)
    {
        var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
        var jitterMs = Random.Shared.Next(0, (int)(baseDelay.TotalMilliseconds * 0.25));
        return baseDelay + TimeSpan.FromMilliseconds(jitterMs);
    }

    private static bool IsTransientError(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.RequestTimeout => true,
            HttpStatusCode.TooManyRequests => true,
            HttpStatusCode.InternalServerError => true,
            HttpStatusCode.BadGateway => true,
            HttpStatusCode.ServiceUnavailable => true,
            HttpStatusCode.GatewayTimeout => true,
            _ when ((int)statusCode >= 500) => true,
            _ => false
        };
    }
}