using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Identity;
using IntegratoR.Abstractions.Common.Results;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

JsonConvert.DefaultSettings = () => new JsonSerializerSettings
{
    Converters = { new ResultJsonConverter(), new ResultGenericJsonConverter() }
};

ArgumentNullException keyVaultUriNotSetException = new("KeyVault URI is not set in environment variables.");

var host = new HostBuilder()
    .ConfigureAppConfiguration((context, config) =>
    {
        var environment = context.HostingEnvironment;

        config.SetBasePath(context.HostingEnvironment.ContentRootPath)
            .AddJsonFile($"{context.HostingEnvironment.EnvironmentName}.settings.json", optional: true, reloadOnChange: true);

        if ((environment.IsDevelopment()))
        {
            config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true);
        }
        config.AddEnvironmentVariables();

        if (!environment.IsDevelopment())
        {
            var keyVaultEnvironmentValue = Environment.GetEnvironmentVariable("ClientSecretKeyVaultURI");
            if (string.IsNullOrEmpty(keyVaultEnvironmentValue))
            {
                throw keyVaultUriNotSetException;
            }
            var keyVaultURI = new Uri(keyVaultEnvironmentValue);
            config.AddAzureKeyVault(keyVaultURI, new DefaultAzureCredential());
        }
    })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var clientAssembly = Assembly.GetExecutingAssembly();

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Configure the Functions Worker's System.Text.Json options so HttpRequestData /
        // HttpResponseData ReadFromJsonAsync / WriteAsJsonAsync accept string-valued enums
        // (e.g. "HierarchyType": "DataEntityLedgerDimensionFormat"). Without this, the worker
        // falls back to the STJ default which only accepts numeric enum values, forcing callers
        // to look up the underlying integer. The worker's default JsonObjectSerializer reads
        // from IOptions<JsonSerializerOptions>, so Configure<JsonSerializerOptions> mutates the
        // live options instance the serializer already holds — no WorkerOptions.Serializer
        // replacement required.
        services.Configure<JsonSerializerOptions>(options =>
        {
            options.Converters.Add(new JsonStringEnumConverter());
        });

        services.AddIntegratoR(context.Configuration, integrator =>
        {
            integrator.AddConsumerHandlers(clientAssembly);
        });
    })
    .Build();

host.Run();
