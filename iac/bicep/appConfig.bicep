param functionAppName string
@secure()
param serviceBusConnectionString string
@secure()
param serviceBusEndpoint string
@secure()
param appInsightsInstrumentationKey string
@secure()
param appInsightsConnectionString string
@secure()
param storageConnectionString string

// Reference the Function App created by AVM
resource functionApp 'Microsoft.Web/sites@2022-09-01' existing = {
  name: functionAppName
}

// Create new app settings that merge existing ones with Service Bus setting
resource appSettings 'Microsoft.Web/sites/config@2022-09-01' = {
  name: 'appsettings'
  parent: functionApp
  properties: {
    // Core Function App settings
    AzureFunctionsJobHost__logging__logLevel__Default: 'Trace'
    AzureFunctionsJobHost__logging__logLevel__Function: 'Trace'
    AzureFunctionsWorker__logging__LogLevel__default: 'Trace'
    'AzureWebJobs.ServiceBusQueueTrigger1.Disabled': 0
    FUNCTIONS_EXTENSION_VERSION: '~4'
    FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'

    // Storage settings
    AzureWebJobsStorage: storageConnectionString
    
    // Application Insights settings (both for compatibility)
    APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
    APPINSIGHTS_INSTRUMENTATIONKEY: appInsightsInstrumentationKey
    
    // Service Bus settings
    redccansbnamespace_SERVICEBUS: serviceBusConnectionString
    ServiceBusConnection: serviceBusEndpoint
  }
}
