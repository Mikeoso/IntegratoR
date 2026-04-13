using System.Reflection;
using System.Text.Json;
using Azure.Identity;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Common.Results.SystemText;
using IntegratoR.RELion.Common.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Converters;
using Microsoft.DurableTask.Worker;
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

        services.Configure<DurableTaskWorkerOptions>(options =>
        {
            JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
            jsonOptions.AddResultConverters();
            options.DataConverter = new JsonDataConverter(jsonOptions);
        });

        services.AddIntegratoR(context.Configuration, integrator =>
        {
            integrator.AddConsumerHandlers(clientAssembly);
        });
        services.AddRelionClient(context.Configuration);
    })
    .Build();

host.Run();
