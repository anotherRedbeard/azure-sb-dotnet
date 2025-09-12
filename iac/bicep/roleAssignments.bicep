param storageAccountName string
param functionAppName string
param principalId string

// Existing storage account reference
resource storageExisting 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

// Role IDs
@description('Storage Blob Data Owner')
var blobRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
@description('Storage Queue Data Contributor')
var queueRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')

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
