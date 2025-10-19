using Azure.Identity;
using DatabaseCloner.FunctionApp.Configuration;
using DatabaseCloner.FunctionApp.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using System.Net.Http.Headers;
using System.Reflection;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, builder) =>
    {
        var env = context.HostingEnvironment;
        builder.AddJsonFile("appsettings.json", optional: true)
               .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
               .AddEnvironmentVariables();

        var config = builder.Build();
        var keyVaultName = config["KeyVaultName"];
        if (!string.IsNullOrWhiteSpace(keyVaultName))
        {
            var vaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
            builder.AddAzureKeyVault(vaultUri, new DefaultAzureCredential());
        }
    })
    .ConfigureServices((context, services) =>
    {
        services.Configure<SqlOptions>(context.Configuration.GetSection("Sql"));
        services.Configure<IngestionOptions>(context.Configuration.GetSection("Ingestion"));
        services.Configure<DatabaseCloneOptions>(context.Configuration.GetSection("DatabaseClone"));
        services.Configure<JobScheduleOptions>(context.Configuration.GetSection("JobSchedules"));

        services.AddSingleton<IAzureSqlManagementClientFactory, AzureSqlManagementClientFactory>();
        services.AddSingleton<IDatabaseCloneService, DatabaseCloneService>();
        services.AddSingleton<IWebIngestionService, WebIngestionService>();
        services.AddSingleton<ISqlWriter, SqlWriter>();

        services.AddHttpClient("web-ingestion", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<IngestionOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(
                new ProductHeaderValue("DatabaseCloner", Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0")));
        })
        .AddPolicyHandler((sp, request) =>
        {
            var retryDelays = Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(2), retryCount: 5);
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(response => (int)response.StatusCode == 429)
                .WaitAndRetryAsync(retryDelays, (outcome, timespan, retryAttempt, context) =>
                {
                    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("HttpRetries");
                    logger.LogWarning(outcome.Exception, "Retrying HTTP request in {Delay}s, attempt {Retry}", timespan.TotalSeconds, retryAttempt);
                });
        });

        services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService("DatabaseCloner.FunctionApp", serviceVersion: Assembly.GetExecutingAssembly().GetName().Version?.ToString());
            })
            .WithTracing(builder =>
            {
                builder.AddSource("DatabaseCloner.FunctionApp");
                builder.AddHttpClientInstrumentation();
            })
            .WithMetrics(builder =>
            {
                builder.AddMeter("DatabaseCloner.FunctionApp");
                builder.AddRuntimeInstrumentation();
            })
            .UseAzureMonitor();
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
        });
    })
    .Build();

await host.RunAsync();
