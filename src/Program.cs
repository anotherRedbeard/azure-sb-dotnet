using Microsoft.Extensions.Hosting;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder()
    .ConfigureFunctionsWebApplication() // Configures the Azure Functions Worker
    .ConfigureServices(services =>
    {
        // Register the Service Bus client
        services.AddSingleton(serviceProvider =>
        {
            var connectionString = Environment.GetEnvironmentVariable("redccansbnamespace_SERVICEBUS");
            return new ServiceBusClient(connectionString);
        });

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