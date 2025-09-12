targetScope = 'subscription'

// Parameters
param subscriptionId string = '0272c02b-5a38-4b6b-86e6-dcc4ff2ff0e8'
param location string = 'canadacentral'
param resourceGroupName string = 'red-ccan-servicebustest-rg'
param serviceBusNamespaceName string = 'red-ccan-sb-namespace'
param queueName string = 'my-test'
param functionAppName string = 'red-ccan-functionapp'
param storageAccountName string = 'redccanfuncappstg012'
@description('Provide the name of the app service plan.')
param aspName string = 'red-ccan-sbtest-asp'

@allowed([
  'EP1'
  'EP2'
  'EP3'
])
@description('The name of the app service plan sku.')
param sku string = 'EP1'

@description('Name of the log analytics workspace.')
param lawName string = 'red-ccan-servicebustest-law'

@description('Name of the app insights resource.')
param appInsightsName string = 'red-ccan-servicebustest-ai'

// Create Resource Group
module rg 'br/public:avm/res/resources/resource-group:0.3.0' = {
  name: 'createResourceGroup'
  scope: subscription(subscriptionId)
  params: {
    name: resourceGroupName
    location: location
  }
}

// Deploy Service Bus Namespace
module serviceBusNamespace 'br/public:avm/res/service-bus/namespace:0.13.2' = {
  name: 'serviceBusNamespace'
  scope: resourceGroup(resourceGroupName)
  dependsOn: [ rg ]
  params: {
    name: serviceBusNamespaceName
    location: location
    skuObject: {
      name: 'Standard'
      capacity: 1
    }
    authorizationRules: [
      {
        name: 'RootManageSharedAccessKey'
        rights: [
          'Listen'
          'Manage'
          'Send'
        ]
      }
    ]
    disableLocalAuth: false
    queues: [
      {
        authorizationRules: [
          {
            name: 'RootManageSharedAccessKey'
            rights: [
              'Listen'
              'Manage'
              'Send'
            ]
          }
        ]
        maxMessageSizeInKilobytes: 2048
        status: 'Active'
        lockDuration: 'PT1M'
        name: queueName
      }
    ]
  }
}

// Send authorization rule
resource rootAuthorizationRule 'Microsoft.ServiceBus/namespaces/queues/authorizationrules@2024-01-01' existing = {
  scope: resourceGroup(resourceGroupName)
  dependsOn: [ serviceBusNamespace ]
  name: '${serviceBusNamespaceName}/${queueName}/RootManageSharedAccessKey'
}

//app service plan
module serverfarm 'br/public:avm/res/web/serverfarm:0.2.2' = {
  scope: resourceGroup(resourceGroupName)
  dependsOn: [ rg ]
  name: 'serverfarmDeployment'
  params: {
    // Required parameters
    name: aspName
    skuCapacity: 1
    skuName: sku
    // Non-required parameters
    kind: 'Elastic'
    maximumElasticWorkerCount: 2
    reserved: true
    location: location
    perSiteScaling: false
    zoneRedundant: false
  }
}

//log analytics workspace resource
module workspace 'br/public:avm/res/operational-insights/workspace:0.5.0' = {
  name: 'workspaceDeployment'
  scope: resourceGroup(resourceGroupName)
  dependsOn: [ rg ]
  params: {
    // Required parameters
    name: lawName
    // Non-required parameters
    location: location
  }
}

//app insights resource
module component 'br/public:avm/res/insights/component:0.4.0' = {
  name: 'componentDeployment'
  scope: resourceGroup(resourceGroupName)
  dependsOn: [ rg ]
  params: {
    // Required parameters
    name: appInsightsName
    workspaceResourceId: workspace.outputs.resourceId
    // Non-required parameters
    location: location
  }
}

//create storage account
module storageAccount 'br/public:avm/res/storage/storage-account:0.20.0' = {
  name: 'storageAccountDeployment'
  scope: resourceGroup(resourceGroupName)
  dependsOn: [ rg ]
  params: {
    // Required parameters
    name: storageAccountName
    // Non-required parameters
    allowBlobPublicAccess: false
    location: location
    skuName: 'Standard_LRS'
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
      ipRules: [ ]
      virtualNetworkRules: [ ]
    }
  }
}

//create function app
module site 'br/public:avm/res/web/site:0.6.0' = {
  name: 'siteDeployment'
  scope: resourceGroup(resourceGroupName)
  dependsOn: [ rg ]
  params: {
    // Required parameters
    kind: 'functionapp'
    name: functionAppName
    serverFarmResourceId: serverfarm.outputs.resourceId
    // Non-required parameters
    appInsightResourceId: component.outputs.resourceId
    location: location
    siteConfig: {
      numberOfWorkers: 1
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      alwaysOn: false
      use32BitWorkerProcess: false
      minimumElasticInstanceCount: 1
    }
    storageAccountResourceId: storageAccount.outputs.resourceId
    // Enable identity-based authentication for AzureWebJobsStorage
    storageAccountUseIdentityAuthentication: true
    // Enable system-assigned managed identity
    managedIdentities: {
      systemAssigned: true
    }
  }
}

// Role assignments for storage access (deployed at resource group scope for proper scoping)
module roleAssignments 'roleAssignments.bicep' = {
  name: 'roleAssignments'
  scope: resourceGroup(resourceGroupName)
  params: {
    storageAccountName: storageAccountName
    functionAppName: functionAppName
    principalId: site.outputs.systemAssignedMIPrincipalId
  }
}


// Deploy app configuration separately via module
module appConfigDeployment 'appConfig.bicep' = {
  name: 'appConfigDeployment'
  scope: resourceGroup(resourceGroupName)
  dependsOn: [site]
  params: {
    functionAppName: functionAppName
    serviceBusEndpoint: serviceBusNamespace.outputs.serviceBusEndpoint
    serviceBusConnectionString: listKeys(rootAuthorizationRule.id, '2024-01-01').primaryConnectionString
    appInsightsInstrumentationKey: component.outputs.instrumentationKey
    appInsightsConnectionString: component.outputs.connectionString
    storageAccountName: storageAccountName
  }
}
