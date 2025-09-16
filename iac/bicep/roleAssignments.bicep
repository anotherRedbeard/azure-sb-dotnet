param storageAccountName string
param functionAppName string
param principalId string
@description('Optional Service Bus namespace name for RBAC (if provided)')
param serviceBusNamespaceName string = ''

// Existing storage account reference
resource storageExisting 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

// Role IDs
@description('Storage Blob Data Owner')
var blobRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
@description('Storage Queue Data Contributor')
var queueRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
@description('Service Bus Data Receiver')
var sbDataReceiverRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0')
// Service Bus Data Sender
@description('Service Bus Data Sender')
var sbDataSenderRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39')

resource blobDataContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageExisting.id, 'blobDataContributor', functionAppName)
  scope: storageExisting
  properties: {
    roleDefinitionId: blobRoleId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

resource queueDataContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageExisting.id, 'queueDataContributor', functionAppName)
  scope: storageExisting
  properties: {
    roleDefinitionId: queueRoleId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

// Conditionally create Service Bus RBAC if namespace name supplied (non-empty)
resource serviceBusNamespaceExisting 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' existing = if (!empty(serviceBusNamespaceName)) {
  name: serviceBusNamespaceName
}

resource serviceBusDataReceiverRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(serviceBusNamespaceName)) {
  name: guid(subscription().id, serviceBusNamespaceName, 'sbDataReceiver', functionAppName)
  scope: serviceBusNamespaceExisting
  properties: {
    roleDefinitionId: sbDataReceiverRoleId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

resource serviceBusDataSenderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(serviceBusNamespaceName)) {
  name: guid(subscription().id, serviceBusNamespaceName, 'sbDataSender', functionAppName)
  scope: serviceBusNamespaceExisting
  properties: {
    roleDefinitionId: sbDataSenderRoleId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
