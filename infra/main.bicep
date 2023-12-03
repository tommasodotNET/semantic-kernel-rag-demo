targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param environmentName string = 'sk-rag-demo'

@minLength(1)
@description('Primary location for all resources')
param location string = 'westeurope'

param principalType string = 'User'

param resourceGroupName string = 'sk-rag-demo'
param keyVaultName string = 'sk-rag-demo'
param containerRegistryName string = 'skragdemo'
param applicationInsightsDashboardName string = 'sk-rag-demo'
param applicationInsightsName string = 'sk-rag-demo'
param logAnalyticsName string = 'sk-rag-demo'

param searchServiceName string = 'skragdemo'
param searchServiceResourceGroupName string = 'fun-with-openai'
param searchServiceResourceGroupLocation string = location

param searchServiceSkuName string = 'standard'
param searchIndexName string = 'sk-rag-demo'

param storageAccountName string = 'skragdemo'
param storageResourceGroupLocation string = location
param storageContainerName string = 'content'

param serviceBusName string = 'skragdemo'
param serviceBusNameResourceGroupLocation string = location

param openAiServiceName string = 'tstocchi'
param openAiResourceGroupName string = 'fun-with-openai'
param openAiResourceGroupLocation string = location
param openAiSkuName string = 'S0'

param formRecognizerServiceName string = 'tstocchi'
param formRecognizerResourceGroupName string = 'fun-with-openai'
param formRecognizerResourceGroupLocation string = location

param formRecognizerSkuName string = 'S0'

param chatGptDeploymentName string = 'gpt-35-turbo'

@description('The resource name of the AKS cluster')
param clusterName string = 'sk-rag-demo'

@description('Id of the user or app to assign application roles')
param principalId string = '86816f6a-2cc0-4620-a9e1-2d43e42162d1'

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
param tags string = ''
var baseTags = { 'azd-env-name': environmentName }
var updatedTags = union(empty(tags) ? {} : base64ToJson(tags), baseTags)

// Organize resources in a resource group
resource resourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: !empty(resourceGroupName) ? '${abbrs.resourcesResourceGroups}${resourceGroupName}' : '${abbrs.resourcesResourceGroups}${environmentName}'
  location: location
  tags: updatedTags
}

resource openAiResourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' existing = if (!empty(openAiResourceGroupName)) {
  name: !empty(openAiResourceGroupName) ? '${abbrs.resourcesResourceGroups}${openAiResourceGroupName}' : resourceGroup.name
}

resource formRecognizerResourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' existing = if (!empty(formRecognizerResourceGroupName)) {
  name: !empty(formRecognizerResourceGroupName) ? '${abbrs.resourcesResourceGroups}${formRecognizerResourceGroupName}' : resourceGroup.name
}

resource searchServiceResourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' existing = if (!empty(searchServiceResourceGroupName)) {
  name: !empty(searchServiceResourceGroupName) ? '${abbrs.resourcesResourceGroups}${searchServiceResourceGroupName}' : resourceGroup.name
}

// Store secrets in a keyvault
module keyVault './core/security/keyvault.bicep' = {
  name: 'keyvault'
  scope: resourceGroup
  params: {
    name: !empty(keyVaultName) ? '${abbrs.keyVaultVaults}${keyVaultName}' : '${abbrs.keyVaultVaults}${resourceToken}'
    location: location
    tags: updatedTags
    principalId: principalId
  }
}

// The AKS cluster to host applications
module aks './core/host/aks.bicep' = {
  name: 'aks'
  scope: resourceGroup
  params: {
    location: location
    name: !empty(clusterName) ? '${abbrs.containerServiceManagedClusters}${clusterName}' : '${abbrs.containerServiceManagedClusters}${resourceToken}'
    containerRegistryName: !empty(containerRegistryName) ? '${abbrs.containerRegistryRegistries}${containerRegistryName}' : '${abbrs.containerRegistryRegistries}${resourceToken}'
    logAnalyticsName: monitoring.outputs.logAnalyticsWorkspaceName
    keyVaultName: keyVault.outputs.name

  }
}

// Monitor application with Azure Monitor
module monitoring 'core/monitor/monitoring.bicep' = {
  name: 'monitoring'
  scope: resourceGroup
  params: {
    location: location
    tags: updatedTags
    logAnalyticsName: !empty(logAnalyticsName) ? '${abbrs.operationalInsightsWorkspaces}${logAnalyticsName}' : '${abbrs.operationalInsightsWorkspaces}${resourceToken}'
    applicationInsightsName: !empty(applicationInsightsName) ? '${abbrs.insightsComponents}${applicationInsightsName}' : '${abbrs.insightsComponents}${resourceToken}'
    applicationInsightsDashboardName: !empty(applicationInsightsDashboardName) ? '${abbrs.portalDashboards}${applicationInsightsDashboardName}' : '${abbrs.portalDashboards}${resourceToken}'
  }
}

module openAi 'core/ai/cognitiveservices.bicep' = {
  name: 'openai'
  scope: openAiResourceGroup
  params: {
    name: !empty(openAiServiceName) ? 'oai-${openAiServiceName}' : '${abbrs.cognitiveServicesAccounts}${resourceToken}'
    location: openAiResourceGroupLocation
    tags: updatedTags
    sku: {
      name: openAiSkuName
    }
  }
}

module formRecognizer 'core/ai/cognitiveservices.bicep' = {
  name: 'formrecognizer'
  scope: formRecognizerResourceGroup
  params: {
    name: !empty(formRecognizerServiceName) ? '${abbrs.cognitiveServicesFormRecognizer}${formRecognizerServiceName}' : '${abbrs.cognitiveServicesFormRecognizer}${resourceToken}'
    kind: 'FormRecognizer'
    location: formRecognizerResourceGroupLocation
    tags: updatedTags
    sku: {
      name: formRecognizerSkuName
    }
  }
}

module searchService 'core/search/search-services.bicep' = {
  name: 'search-service'
  scope: searchServiceResourceGroup
  params: {
    name: !empty(searchServiceName) ? '${abbrs.searchSearchServices}${searchServiceName}' : 'gptkb-${resourceToken}'
    location: searchServiceResourceGroupLocation
    tags: updatedTags
    authOptions: {
      aadOrApiKey: {
        aadAuthFailureMode: 'http401WithBearerChallenge'
      }
    }
    sku: {
      name: searchServiceSkuName
    }
    semanticSearch: 'free'
  }
}

module storage 'core/storage/storage-account.bicep' = {
  name: 'storage'
  scope: resourceGroup
  params: {
    name: !empty(storageAccountName) ? '${abbrs.storageStorageAccounts}${storageAccountName}' : '${abbrs.storageStorageAccounts}${resourceToken}'
    location: storageResourceGroupLocation
    tags: updatedTags
    publicNetworkAccess: 'Enabled'
    sku: {
      name: 'Standard_ZRS'
    }
    deleteRetentionPolicy: {
      enabled: true
      days: 2
    }
    containers: [
      {
        name: storageContainerName
        publicAccess: 'Blob'
      }
    ]
  }
}

module serviceBus 'core/storage/servicebus-topic.bicep' = {
  name: 'servicebus'
  scope: resourceGroup
  params: {
    name: !empty(storageAccountName) ? '${abbrs.serviceBusNamespaces}${serviceBusName}' : '${abbrs.serviceBusNamespaces}${resourceToken}'
    location: storageResourceGroupLocation
    tags: updatedTags
    sku: {
      name: 'Premium'
    }
  }
}

module keyVaultSecrets './core/security/keyvault-secrets.bicep' = {
  scope: resourceGroup
  name: 'keyvault-secrets'
  dependsOn: [keyVault]
  params: {
    keyVaultName: keyVault.outputs.name
    tags: updatedTags
    secrets: [
      {
        name: 'AzureOpenAiChatGptDeployment'
        value: chatGptDeploymentName
      }
      {
        name: 'AzureOpenAiServiceEndpoint'
        value: openAi.outputs.endpoint
      }
      {
        name: 'AzureOpenAiServiceKey'
        value: openAi.outputs.primaryAccessKey
      }
      {
        name: 'AzureSearchServiceEndpoint'
        value: searchService.outputs.endpoint
      }
      {
        name: 'AzureSearchIndex'
        value: searchIndexName
      }
      {
        name: 'AzureSearchServiceKey'
        value: searchService.outputs.primaryAccessKey
      }
      {
        name: 'FormRecognizerEndpoint'
        value: formRecognizer.outputs.endpoint
      }
      {
        name: 'AzureStorageAccountEndpoint'
        value: storage.outputs.primaryEndpoints.blob
      }
      {
        name: 'AzureStorageAccount'
        value: storage.outputs.name
      }
      {
        name: 'AzureStorageAccountKey'
        value: storage.outputs.primaryAccessKey
      }
      {
        name: 'AzureStorageContainer'
        value: storageContainerName
      }
      {
        name: 'ServiceBusConnectionString'
        value: serviceBus.outputs.primaryConnectionString
      }
    ]
  }
}

// USER ROLES
module openAiRoleUser 'core/security/role.bicep' = {
  scope: openAiResourceGroup
  name: 'openai-role-user'
  params: {
    principalId: principalId
    roleDefinitionId: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
    principalType: principalType
  }
}

module formRecognizerRoleUser 'core/security/role.bicep' = {
  scope: formRecognizerResourceGroup
  name: 'formrecognizer-role-user'
  params: {
    principalId: principalId
    roleDefinitionId: 'a97b65f3-24c7-4388-baec-2e87135dc908'
    principalType: principalType
  }
}

module storageRoleUser 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'storage-role-user'
  params: {
    principalId: principalId
    roleDefinitionId: '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
    principalType: principalType
  }
}

module storageContribRoleUser 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'storage-contribrole-user'
  params: {
    principalId: principalId
    roleDefinitionId: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
    principalType: principalType
  }
}

module searchRoleUser 'core/security/role.bicep' = {
  scope: searchServiceResourceGroup
  name: 'search-role-user'
  params: {
    principalId: principalId
    roleDefinitionId: '1407120a-92aa-4202-b7e9-c0e197c71c8f'
    principalType: principalType
  }
}

module searchContribRoleUser 'core/security/role.bicep' = {
  scope: searchServiceResourceGroup
  name: 'search-contrib-role-user'
  params: {
    principalId: principalId
    roleDefinitionId: '8ebe5a00-799e-43f5-93ac-243d3dce84a7'
    principalType: principalType
  }
}

// SYSTEM IDENTITIES
module openAiRoleBackend 'core/security/role.bicep' = {
  scope: openAiResourceGroup
  name: 'openai-role-backend'
  params: {
    principalId: aks.outputs.clusterIdentity.objectId
    roleDefinitionId: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
    principalType: 'ServicePrincipal'
  }
}

module formRecognizerRoleBackend 'core/security/role.bicep' = {
  scope: formRecognizerResourceGroup
  name: 'formrecognizer-role-backend'
  params: {
    principalId: aks.outputs.clusterIdentity.objectId
    roleDefinitionId: 'a97b65f3-24c7-4388-baec-2e87135dc908'
    principalType: 'ServicePrincipal'
  }
}

module storageRoleBackend 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'storage-role-backend'
  params: {
    principalId: aks.outputs.clusterIdentity.objectId
    roleDefinitionId: '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
    principalType: 'ServicePrincipal'
  }
}

module searchRoleBackend 'core/security/role.bicep' = {
  scope: searchServiceResourceGroup
  name: 'search-role-backend'
  params: {
    principalId: aks.outputs.clusterIdentity.objectId
    roleDefinitionId: '1407120a-92aa-4202-b7e9-c0e197c71c8f'
    principalType: 'ServicePrincipal'
  }
}

module searchContribRoleBackend 'core/security/role.bicep' = {
  scope: searchServiceResourceGroup
  name: 'search-contrib-role-backend'
  params: {
    principalId: aks.outputs.clusterIdentity.objectId
    roleDefinitionId: '8ebe5a00-799e-43f5-93ac-243d3dce84a7'
    principalType: 'ServicePrincipal'
  }
}

output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_RESOURCE_GROUP string = resourceGroup.name

output AZURE_OPENAI_RESOURCE_GROUP string = openAiResourceGroup.name
output AZURE_OPENAI_CHATGPT_DEPLOYMENT string = chatGptDeploymentName

output AZURE_FORMRECOGNIZER_SERVICE string = formRecognizer.outputs.name
output AZURE_FORMRECOGNIZER_SERVICE_ENDPOINT string = formRecognizer.outputs.endpoint
output AZURE_FORMRECOGNIZER_RESOURCE_GROUP string = formRecognizerResourceGroup.name

output AZURE_SEARCH_INDEX string = searchIndexName
output AZURE_SEARCH_SERVICE string = searchService.outputs.name
output AZURE_SEARCH_SERVICE_RESOURCE_GROUP string = searchServiceResourceGroup.name
output AZURE_SEARCH_SERVICE_ENDPOINT string = searchService.outputs.endpoint

output AZURE_STORAGE_ACCOUNT string = storage.outputs.name
output AZURE_STORAGE_CONTAINER string = storageContainerName
output AZURE_STORAGE_RESOURCE_GROUP string = resourceGroup.name
output AZURE_STORAGE_BLOB_ENDPOINT string = storage.outputs.primaryEndpoints.blob

output APPLICATIONINSIGHTS_CONNECTION_STRING string = monitoring.outputs.applicationInsightsConnectionString
output APPLICATIONINSIGHTS_NAME string = monitoring.outputs.applicationInsightsName
output AZURE_KEY_VAULT_ENDPOINT string = keyVault.outputs.endpoint
output AZURE_KEY_VAULT_NAME string = keyVault.outputs.name

output AZURE_AKS_CLUSTER_NAME string = aks.outputs.clusterName
output AZURE_AKS_IDENTITY_CLIENT_ID string = aks.outputs.clusterIdentity.clientId
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = aks.outputs.containerRegistryLoginServer
output AZURE_CONTAINER_REGISTRY_NAME string = aks.outputs.containerRegistryName
