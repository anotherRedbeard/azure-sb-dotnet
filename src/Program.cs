using Microsoft.Extensions.Hosting;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder()
    .ConfigureFunctionsWebApplication() // Configures the Azure Functions Worker
    .ConfigureServices(services =>
    {
    // Register Memory Cache for in-memory caching
    services.AddMemoryCache();
    // NOTE: Removed custom ServiceBusClient registration since trigger already handles connection
    // and injected client was unused, causing failures when env vars not set.

        // Enable Application Insights
        services.ConfigureFunctionsApplicationInsights();
        services.AddApplicationInsightsTelemetryWorkerService();
        services.Configure<LoggerFilterOptions>(options =>
        {
            options.Rules.Clear(); // Clear existing rules
        });
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.AddConfiguration(context.Configuration.GetSection("AzureFunctionsWorker:Logging"));
    })
    .Build();

host.Run();