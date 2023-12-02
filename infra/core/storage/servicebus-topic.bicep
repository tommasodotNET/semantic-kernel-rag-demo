param name string
param location string = resourceGroup().location
param tags object = {}
param sku object = { name: 'Premium' }

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-01-01-preview' = {
  name: name
  location: location
  tags: tags
  sku: sku
  properties: {
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
    zoneRedundant: true
  }
}

resource documentProcessingTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  name: 'documentprocess'
  parent: serviceBusNamespace
}

resource knowledgeProcessingTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  name: 'knowledgeprocess'
  parent: serviceBusNamespace
}

output primaryConnectionString string = listKeys('${serviceBusNamespace.id}/authorizationRules/RootManageSharedAccessKey', serviceBusNamespace.apiVersion).primaryConnectionString
