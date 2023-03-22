param name string

// Event Hub
@description('Specifies the messaging tier for Event Hub Namespace.')
@allowed([
  'Basic'
  'Standard'
])
param eventHubSku string = 'Basic'

resource eventHubNamespace 'Microsoft.EventHub/namespaces@2021-11-01' = {
  name: '${name}ns'
  location: resourceGroup().location
  sku: {
    name: eventHubSku
    tier: eventHubSku
    capacity: 1
  }
  properties: {
    isAutoInflateEnabled: false
    maximumThroughputUnits: 0
  }
}

resource eventHub 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: '${name}hub'
  properties: {
    messageRetentionInDays: 1
    partitionCount: 1
  }
}

resource eventHubSendPolicy 'Microsoft.EventHub/namespaces/eventhubs/authorizationRules@2021-01-01-preview' = {
  parent: eventHub
  name: 'sendpolicy'
  properties: {
    rights: [
      'Send'
    ]
  }
}

resource eventHubListenPolicy 'Microsoft.EventHub/namespaces/eventhubs/authorizationRules@2021-01-01-preview' = {
  parent: eventHub
  name: 'listenpolicy'
  properties: {
    rights: [
      'Listen'
    ]
  }
  dependsOn: [
    eventHubSendPolicy
  ]
}
